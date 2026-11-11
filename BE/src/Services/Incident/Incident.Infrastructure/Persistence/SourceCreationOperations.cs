using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Google.Protobuf;
using Grpc.Core;
using MedEquipSLA.Application.Coordination;
using MedEquipSLA.Contracts.Grpc.Equipment.V1;
using MedEquipSLA.Incident.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace MedEquipSLA.Incident.Infrastructure.Persistence;

public sealed class SourceCreationOperations(IncidentDbContext db, EquipmentLookup.EquipmentLookupClient equipment,
    IConfiguration config, TimeProvider time) : ISourceCreationOperations
{
    public async Task<IReadOnlyList<SourceOperationView>> ListAsync(Guid? equipment, int page, CancellationToken ct)
    {
        if (page is < 1 or > 1000000) throw new ArgumentOutOfRangeException(nameof(page));
        return (await db.IdempotencyRequests.AsNoTracking().Where(x => x.OperationName == "CreateIncident" &&
            (equipment == null || x.EquipmentId == equipment)).OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.OperationId)
            .Skip((page - 1) * 50).Take(50).ToArrayAsync(ct)).Select(View).ToArray();
    }
    public async Task<SourceOperationView?> FindAsync(Guid id, CancellationToken ct) =>
        await db.IdempotencyRequests.AsNoTracking().SingleOrDefaultAsync(x => x.OperationId == id &&
            x.OperationName == "CreateIncident", ct) is { } op ? View(op) : null;

    public async Task<(string Status, SourceOperationView? Value)> ExecuteAsync(Guid id, SourceOperationCommand input, CancellationToken ct)
    {
        if (input.Action is not ("Cancel" or "Retry") || input.Actor == Guid.Empty || input.CommandId == Guid.Empty ||
            input.RowVersion.Length != 8 || input.Reason.Trim().Length is < 1 or > 2000) return ("Conflict", null);
        var header = await db.IdempotencyRequests.AsNoTracking().SingleOrDefaultAsync(x => x.OperationId == id &&
            x.OperationName == "CreateIncident", ct);
        if (header is null) return ("NotFound", null);
        if (header.EquipmentId is not { } asset) return ("Conflict", null); // Unknown legacy ownership is never guessed.
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { id, input })));
        var audit = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"IncidentCreationAdmin:{input.Actor}:{input.CommandId}")).AsSpan(0,16));
        db.ChangeTracker.Clear();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var gate = await EquipmentGateAccess.LockAsync(db, asset, ct, time.GetUtcNow());
        var op = await LockedAsync(id, ct);
        var key = $"SourceCommand:{audit:N}";
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @result int;
            EXEC @result=sp_getapplock @Resource={key},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=5000;
            IF @result<0 THROW 51037,'Source operation command lock unavailable.',1;
            """, ct);
        var prior = await db.AuditEntries.AsNoTracking().SingleOrDefaultAsync(x => x.Id == audit, ct);
        if (prior?.ChangesJson is { } json)
        {
            using var replay = JsonDocument.Parse(json);
            return replay.RootElement.GetProperty("hash").GetString() == hash
                ? ("Saved", replay.RootElement.GetProperty("response").Deserialize<SourceOperationView>())
                : ("Conflict", null);
        }
        if (gate.Phase is "Sealed" or "Retired" || op.State != "Pending" || op.ResourceId is not null ||
            !op.RowVersion.SequenceEqual(input.RowVersion) || await db.Incidents.AnyAsync(x => x.Id == id, ct)) return ("Conflict", null);
        var now = time.GetUtcNow();
        if (input.Action == "Cancel") op.RequestCancellation(input.Actor, input.Reason, now); else op.AuthorizeRetry(now);
        await db.SaveChangesAsync(ct);
        var response = View(op);
        db.Add(AuditEntry.ForCreationOperation(id, input.Action == "Cancel" ? "CancelRequested" : "AdminRetryApplied", now,
            input.CommandId.ToString(), JsonSerializer.Serialize(new { hash, input.Reason, response }), input.Actor, audit));
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return ("Saved", response);
    }

    public async Task<int> ReconcileCancellationsAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var candidates = await db.IdempotencyRequests.AsNoTracking().Where(x => x.OperationName == "CreateIncident" &&
            x.State == "Pending" && x.EquipmentId != null && x.CancelIntentId != null && !x.InterventionRequired &&
            (x.NextRetryAtUtc == null || x.NextRetryAtUtc <= now) && (x.LeaseUntilUtc == null || x.LeaseUntilUtc <= now))
            .OrderBy(x => x.NextRetryAtUtc).ThenBy(x => x.OperationId).Select(x => new { x.OperationId, x.EquipmentId }).Take(50).ToArrayAsync(ct);
        var total = 0;
        foreach (var candidate in candidates)
        {
            now = time.GetUtcNow();
            db.ChangeTracker.Clear();
            var owner = Guid.NewGuid().ToString("N");
            long fence; Guid cancel; Guid attempt; byte[] hash;
            await using (var prepare = await db.Database.BeginTransactionAsync(ct))
            {
                await EquipmentGateAccess.LockAsync(db, candidate.EquipmentId!.Value, ct, now);
                var op = await LockedAsync(candidate.OperationId, ct);
                if (op.CancelIntentId is null || op.ResourceId is not null || await db.Incidents.AnyAsync(x => x.Id == op.OperationId, ct)) continue;
                var previousAttempt = op.CurrentAttemptId;
                var previousOwner = op.LeaseOwner;
                if (!op.Claim(owner, now))
                { await db.SaveChangesAsync(ct); await prepare.CommitAsync(ct); continue; }
                if (previousOwner is not null && previousAttempt is not null)
                    db.Add(AuditEntry.ForCreationOperation(op.OperationId, "RecoveryAfterCrash", now, op.CurrentAttemptId!.Value.ToString(),
                        JsonSerializer.Serialize(new { previousAttempt, result = "Unknown" })));
                fence = op.FencingToken; cancel = op.CancelIntentId.Value; attempt = op.CurrentAttemptId!.Value; hash = op.RequestHash.ToArray();
                db.Add(AuditEntry.ForCreationOperation(op.OperationId, "AbortAttemptStarted", now, attempt.ToString(),
                    JsonSerializer.Serialize(new { attempt, fence, cancel, op.TotalAttemptCount })));
                await db.SaveChangesAsync(ct); await prepare.CommitAsync(ct);
            }
            ResourceUseReply? proof = null; string? error = null;
            try
            {
                var key = config["Grpc:RetirementSourceKey"];
                if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("Source credential missing.");
                proof = await equipment.AbortResourceUseAsync(new() { EquipmentId = candidate.EquipmentId.ToString(),
                    OperationId = candidate.OperationId.ToString(), ResourceType = "Incident",
                    RequestHash = ByteString.CopyFrom(hash), CancelIntentId = cancel.ToString(), FencingToken = fence,
                    CorrelationId = attempt.ToString() }, new Metadata { { "x-retirement-source", "Incident" },
                        { "x-retirement-source-key", key } }, DateTime.UtcNow.AddSeconds(3), ct);
                if (proof.EquipmentId != candidate.EquipmentId.ToString() || proof.OperationId != candidate.OperationId.ToString() ||
                    proof.CancelIntentId != cancel.ToString() || proof.State != "Aborted" || proof.FencingToken <= 0 ||
                    proof.FencingToken > fence || !proof.RequestHash.ToByteArray().SequenceEqual(hash) ||
                    !Guid.TryParse(proof.RegistrationId, out var registration) || registration == Guid.Empty)
                { proof = null; error = "AbortProofMismatch"; }
            }
            catch (Exception failure) when (!ct.IsCancellationRequested && failure is RpcException or InvalidOperationException)
            { error = failure is RpcException rpc ? rpc.StatusCode.ToString() : failure.GetType().Name; }
            db.ChangeTracker.Clear();
            await using var final = await db.Database.BeginTransactionAsync(ct);
            await EquipmentGateAccess.LockAsync(db, candidate.EquipmentId!.Value, ct, time.GetUtcNow());
            var current = await LockedAsync(candidate.OperationId, ct);
            var action = "StaleResultIgnored";
            if (current.Owns(owner, fence, time.GetUtcNow()) && current.CancelIntentId == cancel)
            {
                if (proof is not null)
                { current.Abort(JsonSerializer.Serialize(new { proof.RegistrationId, proof.State, proof.FencingToken, proof.CancelIntentId }), time.GetUtcNow()); action = "AbortConfirmed"; total++; }
                else { current.Defer(error ?? "Unknown", time.GetUtcNow()); action = "AbortResultUnknown"; }
            }
            db.Add(AuditEntry.ForCreationOperation(current.OperationId, action, time.GetUtcNow(), attempt.ToString(),
                JsonSerializer.Serialize(new { attempt, fence, cancel, error })));
            await db.SaveChangesAsync(ct); await final.CommitAsync(ct);
        }
        return total;
    }
    private Task<IdempotencyRequest> LockedAsync(Guid id, CancellationToken ct) => db.IdempotencyRequests.FromSqlInterpolated(
        $"SELECT * FROM dbo.IdempotencyRequests WITH (UPDLOCK,HOLDLOCK) WHERE OperationId={id}").SingleAsync(ct);
    private static SourceOperationView View(IdempotencyRequest op) => new(op.OperationId, op.EquipmentId, op.State, op.AttemptCount,
        op.TotalAttemptCount, op.InterventionRequired, op.CancelIntentId != null, op.LastError, op.RowVersion.ToArray());
}
