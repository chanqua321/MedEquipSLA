using System.Security.Cryptography;
using System.Text.Json;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using MedEquipSLA.Contracts.Events;
using MedEquipSLA.Contracts.Grpc.Equipment.V1;
using MedEquipSLA.Maintenance.Application.Features.WorkOrders;
using MedEquipSLA.Maintenance.Domain;
using MedEquipSLA.Maintenance.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WireRequest = MedEquipSLA.Contracts.Grpc.Equipment.V1.WorkOrderRegistrationRequest;

namespace MedEquipSLA.Maintenance.Infrastructure.Persistence;

public sealed class WorkOrderGeneration(MaintenanceDbContext db, EquipmentLookup.EquipmentLookupClient equipment,
    IConfiguration config, TimeProvider time) : IWorkOrderGeneration
{
    private sealed record Claim(Guid Operation, WorkOrderRegistrationPayload Payload, string Owner, long Fence,
        Guid Attempt, Guid? CancelIntent);

    public async Task<int> RunBatchAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var candidates = await (from occurrence in db.ScheduleOccurrences.AsNoTracking()
            join schedule in db.MaintenanceSchedules on occurrence.ScheduleId equals schedule.Id
            where occurrence.State == "Planned" && schedule.IsActive && occurrence.ScheduledOccurrenceAtUtc <= now &&
                !db.EquipmentRetirementGates.Any(g => g.EquipmentId == schedule.EquipmentId && g.Phase != "Open") &&
                !db.IdempotencyRequests.Any(x => x.OccurrenceId == occurrence.Id)
            orderby occurrence.ScheduledOccurrenceAtUtc, occurrence.Id
            select new { schedule.EquipmentId, Schedule = schedule.Id, Occurrence = occurrence.Id }).Take(50).ToArrayAsync(ct);
        foreach (var candidate in candidates)
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var gate = await EquipmentGateAccess.LockAsync(db, candidate.EquipmentId, ct, now);
            if (gate.Phase != "Open") continue;
            var schedule = await db.MaintenanceSchedules.FromSqlInterpolated(
                $"SELECT * FROM dbo.MaintenanceSchedules WITH (UPDLOCK,HOLDLOCK) WHERE Id={candidate.Schedule}").SingleAsync(ct);
            var occurrence = await db.ScheduleOccurrences.FromSqlInterpolated(
                $"SELECT * FROM dbo.ScheduleOccurrences WITH (UPDLOCK,HOLDLOCK) WHERE Id={candidate.Occurrence}").SingleAsync(ct);
            if (!schedule.IsActive || occurrence.State != "Planned" || occurrence.ScheduledOccurrenceAtUtc > now ||
                await db.IdempotencyRequests.AnyAsync(x => x.OccurrenceId == occurrence.Id, ct)) continue;
            gate.AdmitNew(now);
            var payload = new WorkOrderRegistrationPayload(schedule.EquipmentId, schedule.Id, occurrence.Id, Guid.NewGuid(),
                schedule.WorkType, occurrence.ScheduledOccurrenceAtUtc, occurrence.DueAtUtc);
            var operation = IdempotencyRequest.ForWorkOrder(payload, now);
            db.Add(operation);
            Audit(operation, "WorkOrderPending", new { occurrence.Id }, now);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        var pending = await db.IdempotencyRequests.AsNoTracking().Where(x => x.OperationName == "CreateWorkOrder" &&
            x.State == "Pending" && !x.InterventionRequired && (x.NextRetryAtUtc == null || x.NextRetryAtUtc <= now) &&
            (x.LeaseUntilUtc == null || x.LeaseUntilUtc <= now)).OrderBy(x => x.NextRetryAtUtc).ThenBy(x => x.OperationId)
            .Select(x => x.OperationId).Take(50).ToArrayAsync(ct);
        var completed = 0;
        foreach (var operation in pending)
        {
            try { if (await ProcessAsync(operation, ct)) completed++; }
            catch (Exception error) when (!ct.IsCancellationRequested && error is JsonException or ArgumentException or InvalidOperationException)
            {
                // A corrupt/legacy payload must remain a retirement blocker without starving unrelated operations.
                db.ChangeTracker.Clear();
                var header = await db.IdempotencyRequests.AsNoTracking().SingleAsync(x => x.OperationId == operation, ct);
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                if (header.EquipmentId is { } equipmentId) await EquipmentGateAccess.LockAsync(db, equipmentId, ct, time.GetUtcNow());
                var row = await db.IdempotencyRequests.FromSqlInterpolated(
                    $"SELECT * FROM dbo.IdempotencyRequests WITH (UPDLOCK,HOLDLOCK) WHERE OperationId={operation}").SingleAsync(ct);
                if (row.State == "Pending" && !row.InterventionRequired)
                {
                    row.RequireIntervention("SourcePayloadOrCoordinationInvalid", time.GetUtcNow());
                    Audit(row, "NeedsIntervention", new { reason = "SourcePayloadOrCoordinationInvalid" }, time.GetUtcNow());
                    await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
                }
            }
        }
        return completed;
    }

    public async Task<bool> ProcessAsync(Guid operation, CancellationToken ct)
    {
        var claim = await ClaimAsync(operation, ct);
        if (claim is null) return false;
        try
        {
            var request = Wire(claim);
            WorkOrderRegistrationReply remote;
            if (claim.CancelIntent is { } cancel)
                remote = await equipment.VerifyAndAbortWorkOrderRegistrationAsync(
                    new() { Registration = request, CancelIntentId = cancel.ToString() }, Credentials(),
                    DateTime.UtcNow.AddSeconds(3), ct);
            else
            {
                // Register is a fenced idempotent lookup/advance as well as the initial insert.
                remote = await equipment.RegisterWorkOrderAsync(request, Credentials(), DateTime.UtcNow.AddSeconds(3), ct);
            }
            if (!Matches(remote, claim)) return await FinishUnknownAsync(claim, "RemoteProofMismatch", ct);
            return await FinalizeAsync(claim, remote, ct);
        }
        catch (Exception error) when (!ct.IsCancellationRequested && error is RpcException or DbUpdateException or InvalidOperationException)
        {
            var code = error is RpcException rpc ? rpc.StatusCode.ToString() : error.GetType().Name;
            await FinishUnknownAsync(claim, code, ct);
            return false;
        }
    }

    private async Task<Claim?> ClaimAsync(Guid id, CancellationToken ct)
    {
        var header = await db.IdempotencyRequests.AsNoTracking().Where(x => x.OperationId == id &&
            x.OperationName == "CreateWorkOrder" && x.State == "Pending").Select(x => new { x.RequestPayloadJson, x.RequestHash }).SingleOrDefaultAsync(ct);
        if (header?.RequestPayloadJson is not { } json) return null;
        var payload = WorkOrderRegistrationPayload.Parse(json);
        if (!header.RequestHash.SequenceEqual(payload.Hash())) throw new InvalidOperationException("Source payload hash mismatch.");
        db.ChangeTracker.Clear();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var (_, _, operation) = await LockAsync(id, payload, ct);
        var previousAttempt = operation.CurrentAttemptId;
        var previousLease = operation.LeaseOwner;
        var now = time.GetUtcNow();
        var owner = Guid.NewGuid().ToString("N");
        if (!operation.Claim(owner, now)) { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return null; }
        if (previousLease is not null && previousAttempt is not null)
            Audit(operation, "RecoveryAfterCrash", new { previousAttempt, result = "Unknown" }, now);
        Audit(operation, "AutomaticAttemptStarted", new { operation.CurrentAttemptId, operation.FencingToken,
            operation.TotalAttemptCount, phase = operation.CancelIntentId is null ? "Register" : "Abort" }, now);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(id, payload, owner, operation.FencingToken, operation.CurrentAttemptId!.Value, operation.CancelIntentId);
    }

    private async Task<bool> FinalizeAsync(Claim claim, WorkOrderRegistrationReply remote, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var (schedule, occurrence, operation) = await LockAsync(claim.Operation, claim.Payload, ct);
        var now = time.GetUtcNow();
        if (!operation.Owns(claim.Owner, claim.Fence, now) || operation.CancelIntentId != claim.CancelIntent)
        {
            Audit(operation, "StaleResultIgnored", new { claim.Attempt, claim.Fence }, now);
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return false;
        }
        if (claim.CancelIntent is not null)
        {
            operation.Abort(JsonSerializer.Serialize(new { registrationId = remote.RegistrationId, remote.State,
                remote.FencingToken, remote.CancelIntentId, requestHash = Convert.ToHexString(remote.RequestHash.ToByteArray()) }), now);
            if (occurrence.State == "Planned") occurrence.Cancel(now);
            Audit(operation, "AbortConfirmed", new { claim.Attempt, remote.RegistrationId }, now);
        }
        else
        {
            if (!schedule.IsActive || occurrence.State != "Planned" || occurrence.ScheduledOccurrenceAtUtc > now)
            {
                operation.Defer("ScheduleInactiveOrOccurrenceUnavailable", now);
                Audit(operation, "AutomaticAttemptResult", new { claim.Attempt, result = "Deferred" }, now);
                await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return false;
            }
            var order = WorkOrder.Schedule(claim.Payload, Guid.Parse(remote.RegistrationId), remote.EquipmentCode, remote.EquipmentName,
                Guid.Parse(remote.DepartmentId), remote.DepartmentName, now);
            db.Add(order); occurrence.MarkWorkOrderCreated();
            operation.CommitWorkOrder(order.Id, JsonSerializer.Serialize(new { order.Id }), now);
            var payload = new WorkOrderLifecycleEvent(order.EquipmentId, order.EquipmentUseRegistrationId, operation.OperationId,
                operation.FencingToken, claim.Payload.CanonicalJson(), order.Status, order.IsBlocking);
            db.Add(new OutboxMessage(new IntegrationEvent(Guid.NewGuid(), WorkOrderLifecycleEvent.Created, 1, "Maintenance",
                order.Id, order.AggregateVersion, now, operation.OperationId.ToString(), JsonSerializer.Serialize(payload)), "WorkOrder"));
            Audit(operation, "WorkOrderCreated", new { claim.Attempt, order.Id, order.EquipmentUseRegistrationId }, now);
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }

    private async Task<bool> FinishUnknownAsync(Claim claim, string error, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var (_, _, operation) = await LockAsync(claim.Operation, claim.Payload, ct);
        var now = time.GetUtcNow();
        if (operation.Owns(claim.Owner, claim.Fence, now))
        {
            operation.Defer(error, now);
            Audit(operation, "AutomaticAttemptResult", new { claim.Attempt, result = "UnknownOrRejected", error }, now);
        }
        else Audit(operation, "StaleResultIgnored", new { claim.Attempt, error }, now);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return false;
    }

    private async Task<(MaintenanceSchedule, ScheduleOccurrence, IdempotencyRequest)> LockAsync(Guid operation,
        WorkOrderRegistrationPayload payload, CancellationToken ct)
    {
        var gate = await EquipmentGateAccess.LockAsync(db, payload.EquipmentId, ct, time.GetUtcNow());
        // Frozen allows already admitted operations to resolve. A real sealed proof excludes pending operations.
        if (gate.Phase is "Sealed" or "Retired") throw new InvalidOperationException("Source gate is terminally closed.");
        var schedule = await db.MaintenanceSchedules.FromSqlInterpolated(
            $"SELECT * FROM dbo.MaintenanceSchedules WITH (UPDLOCK,HOLDLOCK) WHERE Id={payload.ScheduleId}").SingleAsync(ct);
        var occurrence = await db.ScheduleOccurrences.FromSqlInterpolated(
            $"SELECT * FROM dbo.ScheduleOccurrences WITH (UPDLOCK,HOLDLOCK) WHERE Id={payload.OccurrenceId}").SingleAsync(ct);
        var pending = await db.IdempotencyRequests.FromSqlInterpolated(
            $"SELECT * FROM dbo.IdempotencyRequests WITH (UPDLOCK,HOLDLOCK) WHERE OperationId={operation}").SingleAsync(ct);
        if (schedule.EquipmentId != payload.EquipmentId || occurrence.ScheduleId != schedule.Id ||
            pending.OccurrenceId != occurrence.Id || pending.EquipmentId != payload.EquipmentId ||
            pending.RequestPayloadJson != payload.CanonicalJson() || !pending.RequestHash.SequenceEqual(payload.Hash()))
            throw new InvalidOperationException("Source operation binding mismatch.");
        return (schedule, occurrence, pending);
    }
    private static bool Matches(WorkOrderRegistrationReply r, Claim c) => r.Found &&
        r.EquipmentId == c.Payload.EquipmentId.ToString() && r.OccurrenceId == c.Payload.OccurrenceId.ToString() &&
        r.OperationId == c.Operation.ToString() && r.RequestHash.ToByteArray().SequenceEqual(c.Payload.Hash()) &&
        (c.CancelIntent is null ? r.FencingToken == c.Fence : r.FencingToken > 0 && r.FencingToken <= c.Fence) &&
        Guid.TryParse(r.RegistrationId, out var registration) && registration != Guid.Empty &&
        Guid.TryParse(r.DepartmentId, out var department) && department != Guid.Empty &&
        (c.CancelIntent is { } cancel ? r.State == "Aborted" && r.CancelIntentId == cancel.ToString() : r.State is "Pending" or "Active");

    private static WireRequest Wire(Claim c) => new()
    {
        EquipmentId = c.Payload.EquipmentId.ToString(), ScheduleId = c.Payload.ScheduleId.ToString(),
        OccurrenceId = c.Payload.OccurrenceId.ToString(), WorkOrderId = c.Payload.WorkOrderId.ToString(),
        OperationId = c.Operation.ToString(), WorkType = c.Payload.WorkType, FencingToken = c.Fence,
        RequestHash = ByteString.CopyFrom(c.Payload.Hash()), ScheduledForUtc = Timestamp.FromDateTimeOffset(c.Payload.ScheduledForUtc),
        DueAtUtc = Timestamp.FromDateTimeOffset(c.Payload.DueAtUtc), CorrelationId = c.Attempt.ToString()
    };
    private Metadata Credentials()
    {
        var key = config["Grpc:MaintenanceWorkOrderKey"];
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("WorkOrder credential is missing.");
        return new() { { "x-maintenance-workorder-key", key } };
    }
    private void Audit(IdempotencyRequest operation, string action, object changes, DateTimeOffset now) =>
        db.AuditEntries.Add(AuditEntry.ForOperation(operation.OperationId, action, now,
            operation.CurrentAttemptId?.ToString() ?? operation.OperationId.ToString(), JsonSerializer.Serialize(changes)));
}
