using System.Security.Cryptography;
using System.Text.Json;
using MedEquipSLA.Contracts.Events;
using MedEquipSLA.Equipment.Application.Features.Retirement;
using MedEquipSLA.Equipment.Domain;
using MedEquipSLA.Equipment.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace MedEquipSLA.Equipment.Infrastructure.Persistence;

public sealed class RetirementStore(EquipmentDbContext db) : IRetirementStore
{
    public async Task<IReadOnlyList<RetirementCandidate>> CandidatesAsync(string? search, int page, CancellationToken ct) =>
        await db.EquipmentAssets.AsNoTracking().Where(x => !x.IsRetired &&
            (search == null || x.AssetCode.Contains(search) || x.Name.Contains(search)) &&
            !db.EquipmentUseRegistrations.Any(r => r.EquipmentId == x.Id && (r.State == "Pending" || r.State == "Active")) &&
            !db.EquipmentStateProjections.Any(p => p.EquipmentId == x.Id && p.IsBlocking) &&
            !db.EquipmentRetirementIntents.Any(i => i.EquipmentId == x.Id && i.State != "Completed" && i.State != "Cancelled"))
            .OrderBy(x => x.AssetCode).ThenBy(x => x.Id).Skip((page - 1) * 50).Take(50)
            .Select(x => new RetirementCandidate(x.Id, x.AssetCode, x.Name, x.OperationalStatus, true)).ToArrayAsync(ct);
    public async Task<IReadOnlyList<RetirementView>> HistoryAsync(Guid equipment, int page, CancellationToken ct) =>
        (await db.EquipmentRetirementIntents.AsNoTracking().Include(x => x.Receipts).Where(x => x.EquipmentId == equipment)
            .OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id).Skip((page - 1) * 50).Take(50).ToArrayAsync(ct)).Select(View).ToArray();
    public async Task<RetirementResult> BeginAsync(Guid equipment, Guid actor, Guid key, string reason,
        string correlation, CancellationToken ct)
    {
        reason = reason.Trim();
        if (equipment == Guid.Empty || actor == Guid.Empty || key == Guid.Empty || reason.Length is < 1 or > 2000)
            return new(RetirementOutcome.Invalid);
        db.ChangeTracker.Clear();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var asset = await LockAsync(equipment, ct);
        if (asset is null) return new(RetirementOutcome.NotFound);
        var hash = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { equipment, actor, reason }));
        var replay = await ReplayAsync(actor, key, "BeginRetirement", hash, ct);
        if (replay is not null) return replay;
        var history = await db.EquipmentRetirementIntents.Where(x => x.EquipmentId == equipment).ToListAsync(ct);
        if (asset.IsRetired || history.Any(x => x.IsActive)) return new(RetirementOutcome.Conflict);
        var now = DateTimeOffset.UtcNow;
        var intent = RetirementTransitions.Begin(asset, history, actor, key, reason, now);
        db.Add(intent);
        db.AuditEntries.Add(new AuditEntry(actor, "RetirementRequested", "EquipmentRetirementIntent", intent.Id,
            now, correlation, JsonSerializer.Serialize(new { equipment, intent.RetirementEpoch, reason })));
        await db.SaveChangesAsync(ct);
        var view = View(intent);
        db.Add(IdempotencyRequest.Completed(actor, "BeginRetirement", key, hash, intent.Id, JsonSerializer.Serialize(view), now));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(RetirementOutcome.Saved, view);
    }

    public async Task<RetirementResult> AdminAsync(Guid equipment, Guid intentId, Guid actor, Guid key, byte[] rowVersion,
        string action, string reason, string correlation, CancellationToken ct)
    {
        reason = reason.Trim();
        if (actor == Guid.Empty || key == Guid.Empty || rowVersion.Length != 8 || action is not ("Continue" or "Cancel") ||
            reason.Length is < 1 or > 2000) return new(RetirementOutcome.Invalid);
        db.ChangeTracker.Clear();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var asset = await LockAsync(equipment, ct);
        if (asset is null) return new(RetirementOutcome.NotFound);
        var name = action + "Retirement";
        var hash = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { equipment, intentId, actor, action, reason, rowVersion }));
        var replay = await ReplayAsync(actor, key, name, hash, ct);
        if (replay is not null) return replay;
        var intent = await db.EquipmentRetirementIntents.Include(x => x.Receipts)
            .SingleOrDefaultAsync(x => x.Id == intentId && x.EquipmentId == equipment, ct);
        if (intent is null) return new(RetirementOutcome.NotFound);
        if (!rowVersion.SequenceEqual(intent.RowVersion) || !intent.IsActive) return new(RetirementOutcome.Conflict);
        var now = DateTimeOffset.UtcNow;
        if (action == "Cancel")
        {
            if (intent.State == "Cancelling") return new(RetirementOutcome.Conflict);
            intent.RequestCancel(actor, reason, now);
        }
        else
        {
            intent.Continue(actor, reason, now);
            if (intent.State == "Ready")
            {
                var registrationBlocker = await db.EquipmentUseRegistrations.AnyAsync(x => x.EquipmentId == equipment &&
                    (x.State == "Pending" || x.State == "Active"), ct);
                var projectionBlocker = await db.EquipmentStateProjections.AnyAsync(x => x.EquipmentId == equipment && x.IsBlocking, ct);
                var unknown = await db.EquipmentUseRegistrations.AnyAsync(x => x.EquipmentId == equipment &&
                    (x.SourceService != "Maintenance" && x.SourceService != "Incident"), ct);
                if (!registrationBlocker && !projectionBlocker && !unknown)
                {
                    db.Add(RetirementTransitions.Complete(asset, intent, false, false, false, now));
                    db.Add(new OutboxMessage(new(Guid.NewGuid(), "EquipmentRetired", 1, "Equipment", equipment,
                        asset.AggregateVersion, now, correlation, JsonSerializer.Serialize(new { equipmentId = equipment, intentId, intent.RetirementEpoch }))));
                }
                // A valid Continue may stay Ready while Equipment-local blockers remain. It is not Retired.
            }
        }
        db.AuditEntries.Add(new AuditEntry(actor, intent.State == "Completed" ? "EquipmentRetired" : "Retirement" + action,
            "EquipmentRetirementIntent", intent.Id, now, correlation, JsonSerializer.Serialize(new { reason, intent.State })));
        await db.SaveChangesAsync(ct);
        var view = View(intent);
        db.Add(IdempotencyRequest.Completed(actor, name, key, hash, intent.Id, JsonSerializer.Serialize(view), now));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(RetirementOutcome.Saved, view);
    }

    public async Task<RetirementResult> ReportAsync(string source, Guid equipment, Guid intentId, long epoch, int coverage,
        long version, string state, Guid? proof, string correlation, CancellationToken ct)
    {
        if (source is not ("Maintenance" or "Incident") || correlation.Length > 100)
            return new(RetirementOutcome.Invalid);
        db.ChangeTracker.Clear();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (await LockAsync(equipment, ct) is null) return new(RetirementOutcome.NotFound);
        var intent = await db.EquipmentRetirementIntents.Include(x => x.Receipts)
            .SingleOrDefaultAsync(x => x.Id == intentId && x.EquipmentId == equipment, ct);
        if (intent is null) return new(RetirementOutcome.NotFound);
        if (epoch != intent.RetirementEpoch || coverage != 1) return new(RetirementOutcome.Conflict);
        var receipt = intent.Receipts.SingleOrDefault(x => x.SourceService == source);
        if (receipt is not null && receipt.SourceGateVersion == version && receipt.SourceState == state &&
            (state == "CancelAck" ? receipt.CancelAcknowledgementId : receipt.ReadyProofId) == proof)
            return new(RetirementOutcome.Saved, View(intent));
        if (!intent.IsActive) return new(RetirementOutcome.Conflict);
        var now = DateTimeOffset.UtcNow;
        try { intent.RecordSource(source, source, equipment, intentId, epoch, coverage, version, state, proof, now); }
        catch (InvalidOperationException) { return new(RetirementOutcome.Conflict); }
        if (intent.State == "Cancelling" &&
            intent.Receipts.Count(x => x.SourceState == "CancelAck" && x.CoverageVersion == 1) == 2)
            intent.ConfirmCancelled(now);
        db.AuditEntries.Add(AuditEntry.ForService(source, "RetirementSourceReported", "EquipmentRetirementIntent", intent.Id,
            now, correlation, JsonSerializer.Serialize(new { source, epoch, coverage, version, state, proof })));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(RetirementOutcome.Saved, View(intent));
    }

    public async Task<RetirementView?> FindAsync(Guid equipment, Guid intent, CancellationToken ct) =>
        await db.EquipmentRetirementIntents.AsNoTracking().Include(x => x.Receipts)
            .SingleOrDefaultAsync(x => x.Id == intent && x.EquipmentId == equipment, ct) is { } found ? View(found) : null;

    public async Task<IReadOnlyList<RetirementView>> ListAsync(int page, CancellationToken ct)
    {
        if (page is < 1 or > 1_000_000) throw new ArgumentOutOfRangeException(nameof(page));
        return (await db.EquipmentRetirementIntents.AsNoTracking().Include(x => x.Receipts)
            .Where(x => x.State != "Cancelled" && x.State != "Completed").OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id)
            .Skip((page - 1) * 50).Take(50).ToListAsync(ct)).Select(View).ToArray();
    }

    private async Task<RetirementResult?> ReplayAsync(Guid actor, Guid key, string action, byte[] hash, CancellationToken ct)
    {
        var resource = $"EquipmentRetirementCommand:{actor:N}:{action}:{key:N}";
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @result int;
            EXEC @result=sp_getapplock @Resource={resource},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=5000;
            IF @result<0 THROW 51032,'Retirement command lock could not be acquired.',1;
            """, ct);
        var row = await db.IdempotencyRequests.AsNoTracking().SingleOrDefaultAsync(x => x.ActorScope == actor.ToString() &&
            x.OperationName == action && x.IdempotencyKey == key.ToString(), ct);
        return row is null ? null : row.RequestHash.SequenceEqual(hash) && row.State == "Committed" && row.ResponseJson is { } response
            ? new(RetirementOutcome.Saved, JsonSerializer.Deserialize<RetirementView>(response))
            : new(RetirementOutcome.Conflict);
    }

    private Task<EquipmentAsset?> LockAsync(Guid equipment, CancellationToken ct) =>
        db.EquipmentAssets.FromSqlInterpolated($"SELECT * FROM dbo.EquipmentAssets WITH (UPDLOCK,HOLDLOCK) WHERE Id={equipment}")
            .SingleOrDefaultAsync(ct);

    private static RetirementView View(EquipmentRetirementIntent intent) =>
        new(intent.Id, intent.EquipmentId, intent.RetirementEpoch, intent.State, intent.Reason, intent.RowVersion.ToArray(),
            intent.Receipts.OrderBy(x => x.SourceService).Select(x => new RetirementReceiptView(x.SourceService, x.SourceState,
                x.CoverageVersion, x.SourceGateVersion, x.ReadyProofId, x.CancelAcknowledgementId, x.ReceivedAtUtc)).ToArray(), intent.ContinueCount);
}
