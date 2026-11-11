using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MedEquipSLA.Maintenance.Application.Features.WorkOrders;
using MedEquipSLA.Maintenance.Domain;
using MedEquipSLA.Maintenance.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace MedEquipSLA.Maintenance.Infrastructure.Persistence;

public sealed class WorkOrderOperationAdmin(MaintenanceDbContext db, TimeProvider time) : IWorkOrderOperationAdmin
{
    public async Task<IReadOnlyList<WorkOrderOperationView>> ListAsync(Guid? equipment, string? state, int page, CancellationToken ct)
    {
        var query = db.IdempotencyRequests.AsNoTracking().Where(x => x.OperationName == "CreateWorkOrder");
        if (equipment is { } id) query = query.Where(x => x.EquipmentId == id);
        if (state is not null) query = query.Where(x => x.State == state);
        return (await query.OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.OperationId)
            .Skip((page - 1) * 50).Take(50).ToArrayAsync(ct)).Select(View).ToArray();
    }
    public async Task<WorkOrderOperationView?> FindAsync(Guid id, CancellationToken ct) =>
        await db.IdempotencyRequests.AsNoTracking().SingleOrDefaultAsync(x => x.OperationId == id &&
            x.OperationName == "CreateWorkOrder", ct) is { } row ? View(row) : null;

    public async Task<OperationCommandResult> ExecuteAsync(Guid id, string action, Guid actor, Guid command, byte[] version,
        string reason, CancellationToken ct)
    {
        reason = reason.Trim();
        if (action is not ("Retry" or "Cancel") || actor == Guid.Empty || command == Guid.Empty ||
            version.Length != 8 || reason.Length is < 1 or > 2000) return new(OperationCommandStatus.Conflict);
        var header = await db.IdempotencyRequests.AsNoTracking().SingleOrDefaultAsync(x => x.OperationId == id &&
            x.OperationName == "CreateWorkOrder", ct);
        if (header?.RequestPayloadJson is not { } json) return new(OperationCommandStatus.NotFound);
        var payload = WorkOrderRegistrationPayload.Parse(json);
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { id, action, actor, command, version, reason })));
        var auditId = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"MaintenanceOperationAdmin:{actor:D}:{command:D}")).AsSpan(0,16));
        db.ChangeTracker.Clear();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var gate = await EquipmentGateAccess.LockAsync(db, payload.EquipmentId, ct, time.GetUtcNow());
        await db.MaintenanceSchedules.FromSqlInterpolated(
            $"SELECT * FROM dbo.MaintenanceSchedules WITH (UPDLOCK,HOLDLOCK) WHERE Id={payload.ScheduleId}").SingleAsync(ct);
        await db.ScheduleOccurrences.FromSqlInterpolated(
            $"SELECT * FROM dbo.ScheduleOccurrences WITH (UPDLOCK,HOLDLOCK) WHERE Id={payload.OccurrenceId}").SingleAsync(ct);
        var operation = await db.IdempotencyRequests.FromSqlInterpolated(
            $"SELECT * FROM dbo.IdempotencyRequests WITH (UPDLOCK,HOLDLOCK) WHERE OperationId={id}").SingleAsync(ct);
        var commandLock = $"OperationAdmin:{auditId:N}";
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @result int;
            EXEC @result=sp_getapplock @Resource={commandLock},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=5000;
            IF @result<0 THROW 51035,'Operation command lock could not be acquired.',1;
            """, ct);
        var prior = await db.AuditEntries.AsNoTracking().SingleOrDefaultAsync(x => x.Id == auditId, ct);
        if (prior?.ChangesJson is { } priorJson)
        {
            using var replay = JsonDocument.Parse(priorJson);
            return replay.RootElement.GetProperty("hash").GetString() == hash
                ? new(OperationCommandStatus.Saved, replay.RootElement.GetProperty("response").Deserialize<WorkOrderOperationView>())
                : new(OperationCommandStatus.Conflict);
        }
        if (gate.Phase is "Sealed" or "Retired" || operation.State != "Pending" || !version.SequenceEqual(operation.RowVersion))
            return new(OperationCommandStatus.Conflict);
        var now = time.GetUtcNow();
        if (action == "Cancel") operation.RequestCancellation(actor, reason, now);
        else operation.AuthorizeRetry(now);
        await db.SaveChangesAsync(ct);
        var view = View(operation);
        db.Add(AuditEntry.ForOperation(operation.OperationId, action == "Cancel" ? "CancelRequested" : "AdminRetryApplied",
            now, command.ToString(), JsonSerializer.Serialize(new { hash, reason, response = view }), actor, auditId));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(OperationCommandStatus.Saved, view);
    }
    private static WorkOrderOperationView View(IdempotencyRequest row) =>
        new(row.OperationId, row.EquipmentId, row.OccurrenceId, row.ResourceId, row.State, row.AttemptCount, row.TotalAttemptCount,
            row.InterventionRequired, row.NextRetryAtUtc, row.LastError, row.CancelIntentId != null, row.RowVersion.ToArray());
}
