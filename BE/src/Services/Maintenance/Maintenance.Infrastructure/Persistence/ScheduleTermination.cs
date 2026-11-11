using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Grpc.Core;
using MedEquipSLA.Contracts.Events;
using MedEquipSLA.Contracts.Grpc.Equipment.V1;
using MedEquipSLA.Maintenance.Application.Features.Schedules;
using MedEquipSLA.Maintenance.Infrastructure.Persistence.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;

namespace MedEquipSLA.Maintenance.Infrastructure.Persistence;

public sealed class ScheduleReleaseCursor { public Guid? LastId { get; set; } }

public sealed class ScheduleTermination(MaintenanceDbContext db, EquipmentLookup.EquipmentLookupClient equipment,
    IConfiguration config, TimeProvider time, ScheduleReleaseCursor cursor) : IScheduleTermination
{
    public async Task<ScheduleResult> ExecuteAsync(Guid id, Guid actor, Guid command, byte[] rowVersion, string reason, bool retryRelease, CancellationToken ct)
    {
        reason = reason.Trim();
        if (actor == Guid.Empty || command == Guid.Empty || rowVersion.Length != 8 || reason.Length is < 1 or > 2000)
            return new(ScheduleStatus.Conflict);
        var header = await db.MaintenanceSchedules.AsNoTracking().Where(x => x.Id == id).Select(x => new { x.EquipmentId }).SingleOrDefaultAsync(ct);
        if (header is null) return new(ScheduleStatus.NotFound);
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { id, actor, command, rowVersion, reason, retryRelease })));
        var audit = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"ScheduleTermination:{actor}:{command}")).AsSpan(0,16));
        db.ChangeTracker.Clear();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await EquipmentGateAccess.LockAsync(db, header.EquipmentId, ct, time.GetUtcNow());
        var schedule = await db.MaintenanceSchedules.FromSqlInterpolated(
            $"SELECT * FROM dbo.MaintenanceSchedules WITH (UPDLOCK,HOLDLOCK) WHERE Id={id}").SingleAsync(ct);
        var key = $"ScheduleEndCommand:{audit:N}";
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @result int;
            EXEC @result=sp_getapplock @Resource={key},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=5000;
            IF @result<0 THROW 51040,'Schedule end command lock unavailable.',1;
            """, ct);
        var prior = await db.AuditEntries.AsNoTracking().SingleOrDefaultAsync(x => x.Id == audit, ct);
        if (prior?.ChangesJson is { } json)
        {
            using var replay = JsonDocument.Parse(json);
            return replay.RootElement.GetProperty("hash").GetString() == hash ?
                new(ScheduleStatus.Saved, replay.RootElement.GetProperty("response").Deserialize<ScheduleDetail>()) : new(ScheduleStatus.Conflict);
        }
        if (!rowVersion.SequenceEqual(schedule.RowVersion)) return new(ScheduleStatus.Conflict);
        var now = time.GetUtcNow();
        try
        {
            if (retryRelease) schedule.AuthorizeReleaseRetry(actor, now);
            else
            {
                schedule.End(actor, reason, now);
                await db.ScheduleOccurrences.Where(x => x.ScheduleId == id && x.State == "Planned" &&
                    !db.WorkOrders.Any(w => w.OccurrenceId == x.Id)).ExecuteUpdateAsync(update =>
                        update.SetProperty(x => x.State, "Cancelled").SetProperty(x => x.CancelledAtUtc, now), ct);
            }
        }
        catch (InvalidOperationException) { return new(ScheduleStatus.Conflict); }
        var payload = new MaintenanceScheduleEvent(schedule.EquipmentId, schedule.EquipmentUseRegistrationId, false, true);
        db.Add(new OutboxMessage(new IntegrationEvent(Guid.NewGuid(), MaintenanceScheduleEvent.Ended, 1, "Maintenance", id,
            schedule.AggregateVersion, now, command.ToString(), JsonSerializer.Serialize(payload))));
        await db.SaveChangesAsync(ct);
        var response = ScheduleReader.Detail(schedule);
        db.Add(AuditEntry.ForScheduleCommand(id, retryRelease ? "ScheduleReleaseRetry" : "ScheduleEnded", actor, now, command.ToString(),
            JsonSerializer.Serialize(new { hash, reason, response }), audit));
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return new(ScheduleStatus.Saved, response);
    }

    public async Task<int> ReleaseBatchAsync(CancellationToken ct)
    {
        var query = db.MaintenanceSchedules.AsNoTracking().Where(x => x.EndedAtUtc != null && x.RegistrationReleasedAtUtc == null);
        if (cursor.LastId is { } lastId) query = query.Where(x => x.Id.CompareTo(lastId) > 0);
        var candidates = await query.OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.EquipmentId }).Take(50).ToArrayAsync(ct);
        cursor.LastId = candidates.Length == 0 ? null : candidates[^1].Id;
        var total = 0;
        foreach (var candidate in candidates)
        {
            db.ChangeTracker.Clear();
            Guid registration; long version; var attempt = Guid.NewGuid();
            await using (var prepare = await db.Database.BeginTransactionAsync(ct))
            {
                await EquipmentGateAccess.LockAsync(db, candidate.EquipmentId, ct, time.GetUtcNow());
                var schedule = await db.MaintenanceSchedules.FromSqlInterpolated(
                    $"SELECT * FROM dbo.MaintenanceSchedules WITH (UPDLOCK,HOLDLOCK) WHERE Id={candidate.Id}").SingleAsync(ct);
                if (schedule.EndedAtUtc is null || schedule.RegistrationReleasedAtUtc is not null ||
                    await db.IdempotencyRequests.AnyAsync(x => x.EquipmentId == candidate.EquipmentId && x.State == "Pending", ct) ||
                    await db.WorkOrders.AnyAsync(x => x.EquipmentId == candidate.EquipmentId &&
                        x.Status != "Closed" && x.Status != "Cancelled" &&
                        (x.OccurrenceId == null || db.ScheduleOccurrences.Any(o => o.Id == x.OccurrenceId && o.ScheduleId == candidate.Id)), ct)) continue;
                version = schedule.AggregateVersion; registration = schedule.EquipmentUseRegistrationId;
                var series = $"{schedule.Id:N}:{version}";
                var sql = db.Database.GetDbConnection().CreateCommand();
                await using (sql)
                {
                    sql.Transaction = prepare.GetDbTransaction();
                    sql.CommandText = """
                        SELECT COUNT(*),MAX(OccurredAtUtc) FROM dbo.AuditEntries WHERE EntityType=N'MaintenanceSchedule'
                            AND EntityId=@id AND Action=N'ScheduleReleaseAttempt' AND JSON_VALUE(ChangesJson,'$.series')=@series
                        """;
                    sql.Parameters.Add(new SqlParameter("@id", schedule.Id.ToString())); sql.Parameters.Add(new SqlParameter("@series", series));
                    await using var reader = await sql.ExecuteReaderAsync(ct);
                    await reader.ReadAsync(ct);
                    var count = reader.GetInt32(0);
                    var last = reader.IsDBNull(1) ? (DateTimeOffset?)null : reader.GetFieldValue<DateTimeOffset>(1);
                    if (count >= 8 || last is { } started && time.GetUtcNow() < started.AddSeconds(Math.Min(300, 5 * (1 << count)))) continue;
                }
                db.Add(AuditEntry.ForScheduleCommand(candidate.Id, "ScheduleReleaseAttempt", null, time.GetUtcNow(), attempt.ToString(),
                    JsonSerializer.Serialize(new { series, attempt, version })));
                await db.SaveChangesAsync(ct); await prepare.CommitAsync(ct);
            }
            ResourceUseReply? proof = null; string? error = null;
            try
            {
                var key = config["Grpc:RetirementSourceKey"];
                if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("Source credential missing.");
                proof = await equipment.ReleaseScheduleUseAsync(new() { EquipmentId = candidate.EquipmentId.ToString(),
                    ScheduleId = candidate.Id.ToString(), RegistrationId = registration.ToString(), SourceVersion = version,
                    CorrelationId = attempt.ToString() }, new Metadata { { "x-retirement-source", "Maintenance" },
                        { "x-retirement-source-key", key } }, DateTime.UtcNow.AddSeconds(3), ct);
                if (proof.State != "Released" || proof.EquipmentId != candidate.EquipmentId.ToString() ||
                    proof.RegistrationId != registration.ToString() || proof.OperationId != candidate.Id.ToString()) { proof = null; error = "ReleaseProofMismatch"; }
            }
            catch (Exception failure) when (!ct.IsCancellationRequested && failure is RpcException or InvalidOperationException)
            { error = failure is RpcException rpc ? rpc.StatusCode.ToString() : failure.GetType().Name; }
            db.ChangeTracker.Clear();
            await using var final = await db.Database.BeginTransactionAsync(ct);
            await EquipmentGateAccess.LockAsync(db, candidate.EquipmentId, ct, time.GetUtcNow());
            var current = await db.MaintenanceSchedules.FromSqlInterpolated(
                $"SELECT * FROM dbo.MaintenanceSchedules WITH (UPDLOCK,HOLDLOCK) WHERE Id={candidate.Id}").SingleAsync(ct);
            if (proof is not null && current.EndedAtUtc is not null && current.RegistrationReleasedAtUtc is null)
            { current.RecordRegistrationRelease(time.GetUtcNow()); total++; }
            db.Add(AuditEntry.ForScheduleCommand(candidate.Id, proof is null ? "ScheduleReleaseUnknown" : "ScheduleRegistrationReleased",
                null, time.GetUtcNow(), attempt.ToString(), JsonSerializer.Serialize(new { attempt, version, error })));
            await db.SaveChangesAsync(ct); await final.CommitAsync(ct);
        }
        return total;
    }
}
