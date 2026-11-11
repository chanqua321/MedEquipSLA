using System.Text.Json;
using MedEquipSLA.Equipment.Application.Features.WorkOrderRegistration;
using MedEquipSLA.Equipment.Domain;
using MedEquipSLA.Equipment.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace MedEquipSLA.Equipment.Infrastructure.Persistence;

public sealed class ResourceRegistrationStore(EquipmentDbContext db) : IResourceRegistrationStore
{
    public async Task<ResourceUseProof?> AbortAsync(ResourceUseAbortRequest request, Guid cancel, long fence, string correlation, CancellationToken ct)
    {
        _ = request.CanonicalJson();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var asset = await db.EquipmentAssets.FromSqlInterpolated(
            $"SELECT * FROM dbo.EquipmentAssets WITH (UPDLOCK,HOLDLOCK) WHERE Id={request.EquipmentId}").SingleOrDefaultAsync(ct);
        if (asset is null) return null;
        var row = await db.EquipmentUseRegistrations.SingleOrDefaultAsync(x => x.SourceService == request.Source &&
            x.SourceOperationId == request.OperationId, ct);
        var unit = await db.Departments.SingleAsync(x => x.Id == asset.DepartmentId, ct);
        var now = DateTimeOffset.UtcNow;
        bool changed;
        if (row is null)
        {
            row = EquipmentUseRegistration.ResourceAbortTombstone(asset, unit.Name, request, cancel, fence, now);
            db.Add(row); changed = true;
        }
        else changed = row.AbortResource(asset, unit.Name, request, cancel, fence, now);
        if (changed)
        {
            db.Add(AuditEntry.ForService(request.Source, "ResourceRegistrationAborted", "EquipmentUseRegistration",
                row.Id, now, correlation, JsonSerializer.Serialize(new { request.OperationId, cancel, fence })));
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
        return View(row);
    }
    public async Task<ResourceUseProof?> ReleaseScheduleAsync(Guid equipment, Guid schedule, Guid registration, long version, string correlation, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (await db.EquipmentAssets.FromSqlInterpolated(
            $"SELECT * FROM dbo.EquipmentAssets WITH (UPDLOCK,HOLDLOCK) WHERE Id={equipment}").SingleOrDefaultAsync(ct) is null) return null;
        var row = await db.EquipmentUseRegistrations.SingleOrDefaultAsync(x => x.Id == registration, ct);
        if (row is null) return null;
        var now = DateTimeOffset.UtcNow;
        if (row.ReleaseSchedule(equipment, schedule, registration, version, now))
        {
            db.Add(AuditEntry.ForService("Maintenance", "ScheduleRegistrationReleased", "EquipmentUseRegistration",
                row.Id, now, correlation, JsonSerializer.Serialize(new { schedule, version })));
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
        return View(row);
    }
    private static ResourceUseProof View(EquipmentUseRegistration row) =>
        new(row.Id, row.EquipmentId, row.SourceOperationId, row.State, row.FencingToken, row.CancelIntentId, row.RequestHash, row.SourceVersion);
}
