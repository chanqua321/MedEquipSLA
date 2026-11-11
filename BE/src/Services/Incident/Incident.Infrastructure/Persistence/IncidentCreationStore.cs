using System.Text.Json;
using MedEquipSLA.Application.Coordination;
using MedEquipSLA.Contracts.Events;
using MedEquipSLA.Incident.Application.Features.Incidents;
using MedEquipSLA.Incident.Domain;
using MedEquipSLA.Incident.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace MedEquipSLA.Incident.Infrastructure.Persistence;

public sealed class IncidentCreationStore(IncidentDbContext db) : IIncidentCreationStore
{
    public async Task<PreparedIncidentCreation> PrepareAsync(CreateIncidentInput input, Guid actor, Guid requestId, bool admin,
        byte[] hash, string payload, CancellationToken ct)
    {
        var actorScope = actor.ToString();
        const string operationName = "CreateIncident";
        var operations = db.IdempotencyRequests.Where(x => x.ActorScope == actorScope &&
            x.OperationName == operationName && x.IdempotencyKey == requestId.ToString());
        IdempotencyRequest operation;

        await using (var prepare = await db.Database.BeginTransactionAsync(ct))
        {
            var gate = await EquipmentGateAccess.LockAsync(db, input.EquipmentId, ct);
            var lockName = $"CreateIncident:{actor:N}:{requestId:N}";
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                DECLARE @result int;
                EXEC @result=sp_getapplock @Resource={lockName},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=5000;
                IF @result<0 THROW 51030,'Incident command lock could not be acquired.',1;
                """, ct);
            var saved = await operations.SingleOrDefaultAsync(ct);
            if (saved is not null && !saved.RequestHash.SequenceEqual(hash)) return new(null, new(IncidentCreationStatus.Conflict));
            if (saved?.State == "Committed" && saved.ResourceId is { } existingId) return new(null, new(IncidentCreationStatus.Created, existingId));
            if (saved is not null && saved.State != "Pending") return new(null, new(IncidentCreationStatus.Conflict));
            if (saved?.CancelIntentId is not null) return new(null, new(IncidentCreationStatus.Conflict));
            var invalid = await ValidateLocalAsync(db, input, admin, saved?.OperationId, ct);
            if (invalid is not null) return new(null, invalid);
            if (await EffectiveSlaAsync(db, input.Priority!, DateTimeOffset.UtcNow, ct) is null)
                return new(null, new(IncidentCreationStatus.Conflict, Message: "Chưa cấu hình SLA có hiệu lực cho mức ưu tiên này."));
            operation = saved ?? IdempotencyRequest.Begin(actorScope, operationName, requestId.ToString(), hash, DateTimeOffset.UtcNow);
            if (saved is null)
            {
                try { gate.AdmitNew(DateTimeOffset.UtcNow); }
                catch (SourceAdmissionClosedException) { return new(null, new(IncidentCreationStatus.Conflict)); }
                db.IdempotencyRequests.Add(operation);
            }
            // Existing unknown legacy Pending is hydrated only by the identical authenticated request.
            operation.BindPendingRequest(input.EquipmentId, payload, hash);
            await db.SaveChangesAsync(ct);
            await prepare.CommitAsync(ct);
        }

        return new(operation.OperationId);
    }

    public async Task<IncidentCreationResult> CommitAsync(CreateIncidentInput input, Guid actor, Guid operationId, bool admin,
        byte[] hash, IncidentEquipmentSnapshot snapshot, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var finalGate = await EquipmentGateAccess.LockAsync(db, input.EquipmentId, ct);
        if (finalGate.Phase is "Sealed" or "Retired") return new(IncidentCreationStatus.Conflict);
        var operation = await db.IdempotencyRequests.FromSqlInterpolated(
            $"SELECT * FROM dbo.IdempotencyRequests WITH (UPDLOCK,HOLDLOCK) WHERE OperationId={operationId}").SingleAsync(ct);
        if (!operation.RequestHash.SequenceEqual(hash) || operation.EquipmentId != input.EquipmentId) return new(IncidentCreationStatus.Conflict);
        if (operation.State == "Committed" && operation.ResourceId is { } replayId) return new(IncidentCreationStatus.Created, replayId);
        if (operation.State != "Pending") return new(IncidentCreationStatus.Conflict);
        if (operation.CancelIntentId is not null) return new(IncidentCreationStatus.Conflict);
        var finalInvalid = await ValidateLocalAsync(db, input, admin, operationId, ct);
        if (finalInvalid is not null) return finalInvalid;
        var now = DateTimeOffset.UtcNow;
        var sla = await EffectiveSlaAsync(db, input.Priority!, now, ct);
        if (sla is null) return new(IncidentCreationStatus.Conflict, Message: "Chưa cấu hình SLA có hiệu lực cho mức ưu tiên này.");
        var incident = IncidentRecord.Open(operation.OperationId, input.EquipmentId, snapshot.EquipmentCode,
            snapshot.EquipmentName, snapshot.DepartmentId, snapshot.DepartmentName,
            snapshot.RegistrationId, actor, input.Title!, input.Description!, input.Priority!, input.RelatedIncidentId, sla, now);
        operation.Commit(incident.Id);
        db.Incidents.Add(incident);
        db.OutboxMessages.Add(new OutboxMessage(new UserNotificationEvent("IncidentCreated", "Admins", null,
            "Incident", incident.Id, incident.DepartmentIdSnapshot, "Có sự cố thiết bị mới",
            $"Thiết bị {incident.EquipmentCodeSnapshot} cần được kiểm tra và phân công xử lý.")
            .Envelope("Incident", incident.AggregateVersion, now, operation.OperationId.ToString())));
        db.IncidentActivities.Add(IncidentActivity.Record(incident.Id, "Created", actor, now, "Open", "Open", null, null));
        db.AuditEntries.Add(AuditEntry.ForIncident(actor, incident.Id, "IncidentCreated", now, operation.OperationId.ToString(),
            JsonSerializer.Serialize(new { incident.EquipmentId, incident.EquipmentUseRegistrationId, incident.Status })));
        db.OutboxMessages.Add(new OutboxMessage(new IntegrationEvent(Guid.NewGuid(), IncidentLifecycleEvent.Created,
            1, "Incident", incident.Id, incident.AggregateVersion, now, operation.OperationId.ToString(),
            JsonSerializer.Serialize(new IncidentLifecycleEvent(incident.EquipmentId, incident.EquipmentUseRegistrationId, incident.Status)))));
        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException) { return new(IncidentCreationStatus.Conflict); }
        return new(IncidentCreationStatus.Created, incident.Id);
    }
    private static async Task<IncidentCreationResult?> ValidateLocalAsync(IncidentDbContext db, CreateIncidentInput input, bool admin,
        Guid? ownOperation, CancellationToken ct)
    {
        if (input.RelatedIncidentId is { } related &&
            !await db.Incidents.AnyAsync(x => x.Id == related && x.EquipmentId == input.EquipmentId, ct))
            return new(IncidentCreationStatus.Invalid);
        var duplicate = await db.Incidents.AnyAsync(x => x.EquipmentId == input.EquipmentId && x.Status != "Closed", ct) ||
            await db.IdempotencyRequests.AnyAsync(x => x.EquipmentId == input.EquipmentId &&
                x.OperationName == "CreateIncident" && x.State == "Pending" && x.OperationId != ownOperation, ct);
        // RelatedIncident also supports closed recurrence. Only Admin may override the open-ticket warning.
        if (duplicate && !(admin && input.RelatedIncidentId is not null))
            return new(IncidentCreationStatus.Conflict, Message: "Thiết bị đã có sự cố hoặc yêu cầu đang chờ. Hãy kiểm tra danh sách sự cố.");
        return null;
    }

    private static Task<SlaPolicyVersion?> EffectiveSlaAsync(IncidentDbContext db, string priority, DateTimeOffset now, CancellationToken ct) =>
        (from policy in db.SlaPolicies
         join version in db.SlaPolicyVersions on policy.Id equals version.SlaPolicyId
         where policy.Priority == priority && policy.IsEnabled && version.EffectiveFromUtc <= now &&
             (version.EffectiveToUtc == null || version.EffectiveToUtc > now)
         orderby version.Version descending
         select version).FirstOrDefaultAsync(ct);

}
