using System.Text.Json;
using MedEquipSLA.Application.Coordination;
using MedEquipSLA.Incident.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace MedEquipSLA.Incident.Infrastructure.Persistence;

/// <summary>All counts and gate mutation share the admission lock and one owner-local SQL transaction.</summary>
public sealed class SourceRetirementGate(IncidentDbContext db, TimeProvider time) : ISourceRetirementGate
{
    public async Task<SourceRetirementObservation> PrepareAsync(Guid equipment, Guid intent, long epoch, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var now = time.GetUtcNow();
        var gate = await EquipmentGateAccess.LockAsync(db, equipment, ct, now);
        var changed = gate.Freeze(intent, epoch, now);
        // Stale Prepare cannot report a new intent as the requested old intent.
        if (gate.CurrentIntentId != intent || gate.HighestRetirementEpoch != epoch)
            throw new InvalidOperationException("Stale retirement binding.");
        var (pending, open, unknown) = await CountAsync(equipment, ct);
        if (gate.Phase == "Frozen" && pending + open + unknown == 0)
            changed |= gate.Seal(false, now);
        if (changed) Audit(gate, gate.Phase == "Sealed" ? "SourceSealed" : "SourceFrozen", now);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Snapshot(gate, pending, open, unknown);
    }

    public async Task<SourceRetirementObservation> ObserveEquipmentDecisionAsync(Guid equipment, Guid intent, long epoch,
        string equipmentDecision, CancellationToken ct)
    {
        if (equipmentDecision is not ("Cancelling" or "Cancelled" or "Completed"))
            throw new ArgumentException("Only a durable bound terminal or cancelling Equipment decision is accepted.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var now = time.GetUtcNow();
        var gate = await EquipmentGateAccess.LockAsync(db, equipment, ct, now);
        var changed = equipmentDecision == "Completed"
            ? gate.ObserveCompleted(intent, epoch, now)
            : gate.AcknowledgeCancellation(intent, epoch, now);
        if (gate.CurrentIntentId != intent || gate.HighestRetirementEpoch != epoch)
            throw new InvalidOperationException("Stale retirement binding.");
        if (changed) Audit(gate, equipmentDecision == "Completed" ? "SourceRetired" : "SourceCancelAcknowledged", now);
        var (pending, open, unknown) = await CountAsync(equipment, ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Snapshot(gate, pending, open, unknown);
    }

    private async Task<(int Pending, int Open, int Unknown)> CountAsync(Guid equipment, CancellationToken ct)
    {
        var pending = await db.IdempotencyRequests.CountAsync(x => x.EquipmentId == equipment &&
            x.State != "Committed" && x.State != "Aborted", ct);
        var open = await db.Incidents.CountAsync(x => x.EquipmentId == equipment && x.Status != "Closed", ct);
        var unknown = await db.IdempotencyRequests.CountAsync(x =>
            x.EquipmentId == null && x.State != "Committed" ||
            x.EquipmentId == equipment && (x.State == "Aborted" && !(x.OperationName == "CreateIncident" &&
                x.CancelIntentId != null && x.FencingToken > 0 && x.ResponseStatusCode == 200 && x.ResponseJson != null) ||
                x.State == "Committed" && (x.OperationName != "CreateIncident" || x.ResourceId == null ||
                    !db.Incidents.Any(a => a.Id == x.ResourceId && a.EquipmentId == equipment))), ct);
        unknown += await db.IdempotencyRequests.CountAsync(x => x.EquipmentId == null && x.State == "Committed" &&
            (x.OperationName != "CreateIncident" || x.ResourceId == null ||
                !db.Incidents.Any(a => a.Id == x.ResourceId)), ct);
        return (pending, open, unknown);
    }

    private static SourceRetirementObservation Snapshot(EquipmentRetirementGate gate, int pending, int open, int unknown) =>
        new(gate.EquipmentId, gate.CurrentIntentId!.Value, gate.HighestRetirementEpoch, "Incident", 1,
            gate.Phase, gate.SourceGateVersion, gate.ReadyProofId, gate.CancelAcknowledgementId, pending, open, unknown);

    private void Audit(EquipmentRetirementGate gate, string action, DateTimeOffset now) =>
        db.AuditEntries.Add(AuditEntry.ForService(action, gate.EquipmentId, now, gate.CurrentIntentId!.Value.ToString(),
            JsonSerializer.Serialize(new { gate.CurrentIntentId, gate.HighestRetirementEpoch, gate.Phase,
                gate.SourceGateVersion, gate.ReadyProofId, gate.CancelAcknowledgementId })));
}
