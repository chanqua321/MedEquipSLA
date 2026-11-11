using System.Security.Cryptography;
using System.Text.Json;
using Grpc.Core;
using MedEquipSLA.Contracts.Events;
using MedEquipSLA.Contracts.Grpc.Equipment.V1;
using MedEquipSLA.Maintenance.Application.Features.Schedules;
using MedEquipSLA.Maintenance.Domain;
using MedEquipSLA.Maintenance.Infrastructure.Persistence.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace MedEquipSLA.Maintenance.Infrastructure.Persistence;

public sealed class ScheduleCommands(MaintenanceDbContext db, EquipmentLookup.EquipmentLookupClient equipmentClient,
    IConfiguration config) : IScheduleCommands
{
    public async Task<ScheduleResult> CreateAsync(Guid equipment, string workType, ScheduleTiming timing, Guid actor,
        Guid key, string authorization, CancellationToken ct)
    {
        var hash = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { equipment, workType, timing }));
        var actorScope = actor.ToString();
        var requestKey = key.ToString();
        var requests = db.IdempotencyRequests.AsNoTracking().Where(x => x.ActorScope == actorScope &&
            x.OperationName == "CreateMaintenanceSchedule" && x.IdempotencyKey == requestKey);
        IdempotencyRequest operation;
        var payload = JsonSerializer.Serialize(new { version = 1, equipment, workType, timing });
        await using (var prepare = await db.Database.BeginTransactionAsync(ct))
        {
            var gate = await EquipmentGateAccess.LockAsync(db, equipment, ct);
            var commandLock = $"ScheduleCreate:{actor:N}:{key:N}";
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                DECLARE @result int;
                EXEC @result=sp_getapplock @Resource={commandLock},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=5000;
                IF @result<0 THROW 51030,'Schedule command lock could not be acquired.',1;
                """, ct);
            var saved = await requests.SingleOrDefaultAsync(ct);
            if (saved is null)
            {
                try { gate.AdmitNew(DateTimeOffset.UtcNow); }
                catch (MedEquipSLA.Application.Coordination.SourceAdmissionClosedException)
                { return new(ScheduleStatus.Conflict); }
                operation = IdempotencyRequest.Begin(actor, key, hash, DateTimeOffset.UtcNow);
                operation.BindPendingRequest(equipment, payload, hash);
                db.IdempotencyRequests.Add(operation);
            }
            else
            {
                operation = saved;
                if (!operation.RequestHash.SequenceEqual(hash)) return new(ScheduleStatus.Conflict, OperationId: operation.OperationId);
                if (operation.State == "Pending")
                {
                    db.Attach(operation);
                    operation.BindPendingRequest(equipment, payload, hash);
                }
            }
            await db.SaveChangesAsync(ct);
            await prepare.CommitAsync(ct);
        }
        var id = operation.OperationId;
        if (!operation.RequestHash.SequenceEqual(hash)) return new(ScheduleStatus.Conflict, OperationId: id);
        if (operation.State != "Pending") return Replay(operation);
        if (operation.CancelIntentId is not null) return new(ScheduleStatus.Conflict, OperationId: id);
        var secret = config["Grpc:MaintenanceRegistrationKey"];
        if (string.IsNullOrWhiteSpace(secret)) return new(ScheduleStatus.Unavailable, OperationId: id);

        RegisterIncidentUseReply reply;
        try
        {
            reply = await equipmentClient.RegisterScheduleUseAsync(new()
            {
                EquipmentId = equipment.ToString(), OperationId = id.ToString()
            }, new Metadata { { "authorization", authorization }, { "x-maintenance-registration-key", secret } },
                deadline: DateTime.UtcNow.AddSeconds(3), cancellationToken: ct);
        }
        catch (RpcException e) when (!ct.IsCancellationRequested)
        {
            var status = e.StatusCode switch
            {
                StatusCode.NotFound => ScheduleStatus.InvalidEquipment,
                StatusCode.FailedPrecondition => ScheduleStatus.Conflict,
                _ => ScheduleStatus.Unavailable
            };
            return new(status, OperationId: id);
        }
        if (!Guid.TryParse(reply.RegistrationId, out var registration) || registration == Guid.Empty ||
            !Guid.TryParse(reply.DepartmentId, out var department) || department == Guid.Empty)
            return new(ScheduleStatus.Unavailable, OperationId: id);

        // The durable pending row predates the RPC. Serialize local finalization only after the remote call returns.
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var finalGate = await EquipmentGateAccess.LockAsync(db, equipment, ct);
        if (finalGate.Phase is "Sealed" or "Retired") return new(ScheduleStatus.Conflict, OperationId: id);
        operation = await db.IdempotencyRequests.FromSqlInterpolated(
            $"SELECT * FROM dbo.IdempotencyRequests WITH (UPDLOCK, HOLDLOCK) WHERE OperationId = {id}").SingleAsync(ct);
        if (operation.State != "Pending") return Replay(operation);
        if (operation.CancelIntentId is not null) return new(ScheduleStatus.Conflict, OperationId: id);

        var now = DateTimeOffset.UtcNow;
        var schedule = MaintenanceSchedule.Create(id, equipment, reply.EquipmentCode, reply.EquipmentName,
            department, reply.DepartmentName, registration, workType, timing.AnchorAtUtc, timing.IntervalDays,
            timing.ReminderLeadMinutes, timing.CompletionWindowMinutes, timing.IsActive, actor, now);
        db.MaintenanceSchedules.Add(schedule);
        Record(schedule, MaintenanceScheduleEvent.Created, actor, id.ToString(), now,
            new { equipment, workType, timing });
        await db.SaveChangesAsync(ct);
        var response = ScheduleReader.Detail(schedule);
        operation.Commit(JsonSerializer.Serialize(response));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(ScheduleStatus.Saved, response, id);
    }

    public async Task<ScheduleResult> UpdateAsync(Guid id, ScheduleTiming timing, byte[] version, Guid actor,
        string correlation, CancellationToken ct)
    {
        var equipment = await db.MaintenanceSchedules.AsNoTracking().Where(x => x.Id == id)
            .Select(x => (Guid?)x.EquipmentId).SingleOrDefaultAsync(ct);
        if (equipment is null) return new(ScheduleStatus.NotFound);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var gate = await EquipmentGateAccess.LockAsync(db, equipment.Value, ct);
        var schedule = await db.MaintenanceSchedules.FromSqlInterpolated(
            $"SELECT * FROM dbo.MaintenanceSchedules WITH (UPDLOCK,HOLDLOCK) WHERE Id={id}").SingleAsync(ct);
        await db.Entry(schedule).ReloadAsync(ct);
        if (schedule.EndedAtUtc is not null) return new(ScheduleStatus.Conflict);
        if (!version.SequenceEqual(schedule.RowVersion)) return new(ScheduleStatus.Conflict);
        var before = ScheduleReader.Detail(schedule);
        if (before.Timing == timing) return new(ScheduleStatus.Saved, before);
        if (gate.Phase == "Retired") return new(ScheduleStatus.Conflict);
        if (!schedule.IsActive && timing.IsActive)
        {
            try { gate.AdmitNew(DateTimeOffset.UtcNow); }
            catch (MedEquipSLA.Application.Coordination.SourceAdmissionClosedException)
            { return new(ScheduleStatus.Conflict); }
        }

        var next = timing.AnchorAtUtc;
        if (schedule.AnchorAtUtc == timing.AnchorAtUtc && schedule.IntervalDays == timing.IntervalDays)
            next = schedule.NextOccurrenceAtUtc;
        else
        {
            var last = await db.ScheduleOccurrences.Where(x => x.ScheduleId == id)
                .OrderByDescending(x => x.ScheduledOccurrenceAtUtc)
                .Select(x => (DateTimeOffset?)x.ScheduledOccurrenceAtUtc).FirstOrDefaultAsync(ct);
            if (last is { } materialized && materialized >= next)
            {
                var period = (long)timing.IntervalDays * TimeSpan.TicksPerDay;
                var periods = (materialized - next).Ticks / period + 1;
                try { next = next.AddTicks(checked(periods * period)); }
                catch (ArgumentOutOfRangeException) { return new(ScheduleStatus.InvalidTiming); }
                catch (OverflowException) { return new(ScheduleStatus.InvalidTiming); }
            }
        }

        if (!ScheduleTimingLimits.IsUsable(timing, next)) return new(ScheduleStatus.InvalidTiming);
        var now = DateTimeOffset.UtcNow;
        try
        {
            schedule.Revise(timing.AnchorAtUtc, next, timing.IntervalDays, timing.ReminderLeadMinutes,
                timing.CompletionWindowMinutes, timing.IsActive, actor, now);
        }
        catch (ArgumentOutOfRangeException) { return new(ScheduleStatus.InvalidTiming); }
        catch (OverflowException) { return new(ScheduleStatus.InvalidTiming); }
        if (!ScheduleTimingLimits.IsUsable(timing, schedule.NextOccurrenceAtUtc)) return new(ScheduleStatus.InvalidTiming);
        var action = before.Timing.IsActive && !timing.IsActive ? MaintenanceScheduleEvent.Deactivated : MaintenanceScheduleEvent.Updated;
        Record(schedule, action, actor, correlation, now, new { before = before.Timing, after = timing });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return new(ScheduleStatus.Conflict); }
        await transaction.CommitAsync(ct);
        return new(ScheduleStatus.Saved, ScheduleReader.Detail(schedule));
    }

    private void Record(MaintenanceSchedule schedule, string action, Guid actor, string correlation, DateTimeOffset now, object changes)
    {
        db.AuditEntries.Add(new AuditEntry(actor, action, schedule.Id, now, correlation, JsonSerializer.Serialize(changes)));
        var payload = new MaintenanceScheduleEvent(schedule.EquipmentId, schedule.EquipmentUseRegistrationId, schedule.IsActive);
        db.OutboxMessages.Add(new OutboxMessage(new IntegrationEvent(Guid.NewGuid(), action, 1, "Maintenance",
            schedule.Id, schedule.AggregateVersion, now, correlation, JsonSerializer.Serialize(payload))));
    }

    private static ScheduleResult Replay(IdempotencyRequest operation) =>
        operation.State == "Committed" && operation.ResponseJson is { } json
            ? new(ScheduleStatus.Saved, JsonSerializer.Deserialize<ScheduleDetail>(json), operation.OperationId)
            : new(ScheduleStatus.Conflict, OperationId: operation.OperationId);
}
