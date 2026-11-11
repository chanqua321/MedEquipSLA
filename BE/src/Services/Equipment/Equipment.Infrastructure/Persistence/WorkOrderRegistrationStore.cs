using System.Security.Cryptography;
using System.Text.Json;
using MedEquipSLA.Equipment.Application.Features.WorkOrderRegistration;
using MedEquipSLA.Equipment.Domain;
using MedEquipSLA.Equipment.Infrastructure.Persistence.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace MedEquipSLA.Equipment.Infrastructure.Persistence;

public sealed class WorkOrderRegistrationStore(EquipmentDbContext db) : IWorkOrderRegistrationStore
{
    public async Task<RegistrationResult> RegisterAsync(WorkOrderRegistrationRequest request, Guid operationId,
        long fence, string correlation, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var asset = await db.EquipmentAssets.FromSqlInterpolated(
            $"SELECT * FROM dbo.EquipmentAssets WITH (UPDLOCK,HOLDLOCK) WHERE Id={request.EquipmentId}").SingleOrDefaultAsync(ct);
        if (asset is null) return new(RegistrationOutcome.NotFound);
        var existing = await db.EquipmentUseRegistrations.SingleOrDefaultAsync(x =>
            x.SourceService == "Maintenance" && x.SourceOperationId == operationId, ct);
        if (existing is not null)
        {
            if (!existing.MatchesWorkOrder(request, operationId)) return new(RegistrationOutcome.Conflict);
            if (existing.State is not ("Pending" or "Active")) return new(RegistrationOutcome.Terminal);
            if (fence < existing.FencingToken) return new(RegistrationOutcome.StaleFence);
            if (asset.IsRetired) return new(RegistrationOutcome.Retired);
            if (existing.ObserveWorkOrderFence(fence, DateTimeOffset.UtcNow)) await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new(RegistrationOutcome.Saved, View(existing));
        }
        if (asset.IsRetired) return new(RegistrationOutcome.Retired);
        if (await db.EquipmentRetirementIntents.AnyAsync(x => x.EquipmentId == asset.Id &&
                x.State != "Cancelled" && x.State != "Completed", ct))
            return new(RegistrationOutcome.AdmissionClosed);
        if (await db.EquipmentUseRegistrations.AnyAsync(x => x.SourceService == "Maintenance" &&
                x.ResourceType == "WorkOrder" && x.ResourceId == request.OccurrenceId, ct))
            return new(RegistrationOutcome.Conflict);
        var department = await db.Departments.SingleAsync(x => x.Id == asset.DepartmentId, ct);
        var now = DateTimeOffset.UtcNow;
        var registration = EquipmentUseRegistration.RegisterWorkOrder(asset, request, operationId, fence, department.Name, now);
        db.Add(registration);
        db.AuditEntries.Add(AuditEntry.ForService("Maintenance", "WorkOrderRegistered", "EquipmentUseRegistration",
            registration.Id, now, correlation, JsonSerializer.Serialize(new { operationId, request.OccurrenceId, fence })));
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException e) when (e.InnerException is SqlException { Number: 2601 or 2627 })
        { return new(RegistrationOutcome.Conflict); }
        await transaction.CommitAsync(ct);
        return new(RegistrationOutcome.Saved, View(registration));
    }

    public async Task<RegistrationResult> FindAsync(Guid equipment, Guid occurrence, Guid operation, byte[] hash, CancellationToken ct)
    {
        var registration = await db.EquipmentUseRegistrations.AsNoTracking().SingleOrDefaultAsync(x =>
            x.SourceService == "Maintenance" && x.SourceOperationId == operation, ct);
        if (registration is null)
            return new(await db.EquipmentAssets.AnyAsync(x => x.Id == equipment, ct)
                ? RegistrationOutcome.Absent : RegistrationOutcome.NotFound);
        if (registration.EquipmentId != equipment || registration.ResourceId != occurrence ||
            registration.ResourceType != "WorkOrder" || registration.RequestHash is not { } stored ||
            !CryptographicOperations.FixedTimeEquals(stored, hash))
            return new(RegistrationOutcome.Conflict);
        return new(RegistrationOutcome.Saved, View(registration));
    }

    public Task<RegistrationResult> ConfirmAsync(WorkOrderRegistrationRequest request, Guid operation,
        Guid registrationId, long fence, long sourceVersion, string correlation, CancellationToken ct) =>
        MutateAsync(request, operation, fence, registrationId, sourceVersion, null, correlation, ct);

    public Task<RegistrationResult> AbortAsync(WorkOrderRegistrationRequest request, Guid operation,
        long fence, Guid cancelIntent, string correlation, CancellationToken ct) =>
        MutateAsync(request, operation, fence, null, 0, cancelIntent, correlation, ct);

    private async Task<RegistrationResult> MutateAsync(WorkOrderRegistrationRequest request, Guid operation,
        long fence, Guid? registrationId, long sourceVersion, Guid? cancelIntent, string correlation, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var asset = await db.EquipmentAssets.FromSqlInterpolated(
            $"SELECT * FROM dbo.EquipmentAssets WITH (UPDLOCK,HOLDLOCK) WHERE Id={request.EquipmentId}").SingleOrDefaultAsync(ct);
        if (asset is null) return new(RegistrationOutcome.NotFound);
        var registration = await db.EquipmentUseRegistrations.SingleOrDefaultAsync(x =>
            x.SourceService == "Maintenance" && x.SourceOperationId == operation, ct);
        var now = DateTimeOffset.UtcNow;
        bool changed;
        if (registration is null)
        {
            if (cancelIntent is null) return new(RegistrationOutcome.NotFound);
            if (await db.EquipmentUseRegistrations.AnyAsync(x => x.SourceService == "Maintenance" &&
                    x.ResourceType == "WorkOrder" && x.ResourceId == request.OccurrenceId, ct))
                return new(RegistrationOutcome.Conflict);
            var department = await db.Departments.SingleAsync(x => x.Id == asset.DepartmentId, ct);
            registration = EquipmentUseRegistration.WorkOrderAbortTombstone(asset, request, operation,
                fence, department.Name, now, cancelIntent.Value);
            db.Add(registration);
            changed = true;
        }
        else
        {
            if (cancelIntent is null && asset.IsRetired && registration.State != "Released")
                return new(RegistrationOutcome.Retired);
            if (!registration.MatchesWorkOrder(request, operation)) return new(RegistrationOutcome.Conflict);
            if (cancelIntent is null && registration.State == "Aborted" ||
                cancelIntent is not null && registration.State is "Active" or "Released")
                return new(RegistrationOutcome.Terminal);
            try
            {
                changed = cancelIntent is { } cancel
                    ? registration.AbortWorkOrder(request, operation, fence, cancel, now)
                    : registration.ConfirmWorkOrder(request, operation, registrationId!.Value, fence, sourceVersion, now);
            }
            catch (StaleWorkOrderFenceException) { return new(RegistrationOutcome.StaleFence); }
            catch (InvalidOperationException) { return new(RegistrationOutcome.Conflict); }
        }
        if (changed)
        {
            db.AuditEntries.Add(AuditEntry.ForService("Maintenance", cancelIntent is null ?
                "WorkOrderRegistrationCommitted" : "WorkOrderRegistrationAborted", "EquipmentUseRegistration",
                registration.Id, now, correlation, JsonSerializer.Serialize(new { operation, fence, sourceVersion, cancelIntent })));
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException e) when (e.InnerException is SqlException { Number: 2601 or 2627 })
            { return new(RegistrationOutcome.Conflict); }
        }
        await transaction.CommitAsync(ct);
        return new(RegistrationOutcome.Saved, View(registration));
    }

    private static RegistrationView View(EquipmentUseRegistration r) => new(r.Id, r.EquipmentId, r.ResourceId,
        r.SourceOperationId, r.State, r.FencingToken, r.RequestHash!, r.EquipmentCodeSnapshot!,
        r.EquipmentNameSnapshot!, r.DepartmentIdSnapshot!.Value, r.DepartmentNameSnapshot!, r.SourceVersion, r.CancelIntentId);
}
