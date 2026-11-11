using System.Data;
using System.Text.Json;
using Grpc.Core;
using MedEquipSLA.Application.Coordination;
using MedEquipSLA.Contracts.Grpc.Equipment.V1;
using MedEquipSLA.Incident.Infrastructure.Persistence;
using MedEquipSLA.Incident.Infrastructure.Persistence.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;

namespace MedEquipSLA.Incident.Infrastructure.Workers;

/// <summary>Source-to-Equipment only. SQL never spans gRPC. Attempt history survives process restart.</summary>
public sealed class RetirementSourceCoordinator(IncidentDbContext db, ISourceRetirementGate gates,
    EquipmentLookup.EquipmentLookupClient equipment, IConfiguration config, TimeProvider time)
{
    public async Task<IReadOnlyList<RetirementIntentReply>> DiscoverAsync(int page, CancellationToken ct)
    {
        var active = (await equipment.ListRetirementIntentsAsync(new() { Page = page }, Credentials(),
            deadline: DateTime.UtcNow.AddSeconds(3), cancellationToken: ct)).Intents.ToList();
        // A source that is already Sealed keeps admission closed until it learns Completed or Cancelling.
        var tracked = await db.EquipmentRetirementGates.AsNoTracking().Where(x => x.CurrentIntentId != null &&
                (x.Phase == "Frozen" || x.Phase == "Sealed")).OrderBy(x => x.EquipmentId)
            .Skip((page - 1) * 50).Take(50).ToArrayAsync(ct);
        foreach (var gate in tracked)
            if (!active.Any(x => x.IntentId == gate.CurrentIntentId.ToString()))
                active.Add(new() { EquipmentId = gate.EquipmentId.ToString(), IntentId = gate.CurrentIntentId!.Value.ToString(),
                    RetirementEpoch = gate.HighestRetirementEpoch, State = "Tracked" });
        return active;
    }

    public async Task<bool> CoordinateAsync(RetirementIntentReply discovered, CancellationToken ct)
    {
        var id = Guid.Parse(discovered.IntentId);
        var asset = Guid.Parse(discovered.EquipmentId);
        var series = $"{id:N}:{discovered.ContinueCount}:{discovered.State}";
        var attempt = Guid.NewGuid();
        try
        {
            // Query again: discovery is not authorization to unfreeze a gate.
            await AuditResultAsync(asset, series, attempt, "RetirementQueryAttempt", new { intent = id }, ct);
            var decision = await equipment.GetRetirementIntentAsync(new() { EquipmentId = asset.ToString(), IntentId = id.ToString() },
                Credentials(), deadline: DateTime.UtcNow.AddSeconds(3), cancellationToken: ct);
            if (decision.EquipmentId != asset.ToString() || decision.IntentId != id.ToString() ||
                decision.RetirementEpoch != discovered.RetirementEpoch)
                throw new InvalidOperationException("Retirement query binding mismatch.");
            await AuditResultAsync(asset, series, attempt, "RetirementQueryResult", new { decision.State, decision.ContinueCount }, ct);
            // Passive status monitoring continues after exhausted action retries. A terminal Equipment decision
            // or an explicit Admin Continue has its own durable series and can recover a previously blocked gate.
            series = $"{id:N}:{decision.ContinueCount}:{decision.State}";
            if (!await BeginAttemptAsync(asset, series, attempt, ct)) return false;
            var observation = decision.State is "Cancelling" or "Cancelled" or "Completed"
                ? await gates.ObserveEquipmentDecisionAsync(asset, id, decision.RetirementEpoch, decision.State, ct)
                : await gates.PrepareAsync(asset, id, decision.RetirementEpoch, ct);
            if (decision.State != "Completed" && observation.Phase != "Retired")
            {
                var state = decision.State is "Cancelling" or "Cancelled" ? "CancelAck" :
                    observation.Phase == "Sealed" ? "Sealed" : observation.Phase == "Frozen" ? "Blocked" : null;
                if (state is not null)
                    await equipment.ReportRetirementSourceStateAsync(new()
                    {
                        EquipmentId = asset.ToString(), IntentId = id.ToString(), RetirementEpoch = observation.Epoch,
                        CoverageVersion = observation.CoverageVersion, SourceGateVersion = observation.GateVersion,
                        SourceState = state, ProofId = (state == "CancelAck" ? observation.CancelAcknowledgementId : observation.ReadyProofId)?.ToString() ?? "",
                        CorrelationId = attempt.ToString()
                    }, Credentials(), deadline: DateTime.UtcNow.AddSeconds(3), cancellationToken: ct);
            }
            await AuditResultAsync(asset, series, attempt, "RetirementCoordinationResult", new { state = observation.Phase }, ct);
            return true;
        }
        catch (Exception error) when (!ct.IsCancellationRequested)
        {
            // Do not log credentials or remote response bodies. Unknown keeps the last durable gate state.
            await AuditResultAsync(asset, series, attempt, "RetirementCoordinationUnknown",
                new { error = error is RpcException rpc ? rpc.StatusCode.ToString() : error.GetType().Name }, ct);
            return false;
        }
    }

    private async Task<bool> BeginAttemptAsync(Guid asset, string series, Guid attempt, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var resource = $"RetirementAttempt:Incident:{asset:N}";
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @result int;
            EXEC @result=sp_getapplock @Resource={resource},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=5000;
            IF @result<0 THROW 51033,'Retirement attempt lock could not be acquired.',1;
            """, ct);
        var connection = db.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = tx.GetDbTransaction();
        command.CommandText = """
            SELECT COUNT(*),MAX(OccurredAtUtc) FROM dbo.AuditEntries
            WHERE EntityType=N'EquipmentRetirementGate' AND EntityId=@equipment
                AND Action=N'RetirementCoordinationAttempt' AND JSON_VALUE(ChangesJson,'$.series')=@series
            """;
        command.Parameters.Add(new SqlParameter("@equipment", asset.ToString()));
        command.Parameters.Add(new SqlParameter("@series", series));
        int count;
        DateTimeOffset? last;
        await using (var reader = await command.ExecuteReaderAsync(ct))
        { await reader.ReadAsync(ct); count = reader.GetInt32(0); last = reader.IsDBNull(1) ? null : reader.GetFieldValue<DateTimeOffset>(1); }
        // Bounded durable retry. Admin Continue changes continue_count and opens a new attempt series.
        if (count >= 8)
        {
            var entity = asset.ToString();
            var exists = await db.AuditEntries.FromSqlInterpolated($"""
                SELECT * FROM dbo.AuditEntries WHERE EntityType=N'EquipmentRetirementGate' AND EntityId={entity}
                    AND Action=N'RetirementNeedsIntervention' AND JSON_VALUE(ChangesJson,'$.series')={series}
                """).AsNoTracking().AnyAsync(ct);
            if (!exists)
            {
                db.AuditEntries.Add(AuditEntry.ForService("RetirementNeedsIntervention", asset, time.GetUtcNow(), attempt.ToString(),
                    JsonSerializer.Serialize(new { series, count, reason = "Action retry limit reached; Admin Continue required." })));
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            return false;
        }
        if (last is { } started && time.GetUtcNow() < started.AddSeconds(Math.Min(300, 5 * (1 << count)))) return false;
        db.AuditEntries.Add(AuditEntry.ForService("RetirementCoordinationAttempt", asset, time.GetUtcNow(), attempt.ToString(),
            JsonSerializer.Serialize(new { series, attempt, number = count + 1 })));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }

    private async Task AuditResultAsync(Guid asset, string series, Guid attempt, string action, object result, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        db.AuditEntries.Add(AuditEntry.ForService(action, asset, time.GetUtcNow(), attempt.ToString(),
            JsonSerializer.Serialize(new { series, attempt, result })));
        await db.SaveChangesAsync(ct);
    }

    private Metadata Credentials()
    {
        var key = config["Grpc:RetirementSourceKey"];
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("Retirement source credential is not configured.");
        return new() { { "x-retirement-source", "Incident" }, { "x-retirement-source-key", key } };
    }
}
