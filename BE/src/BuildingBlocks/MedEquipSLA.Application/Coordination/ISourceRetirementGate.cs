namespace MedEquipSLA.Application.Coordination;

public sealed record SourceRetirementObservation(Guid EquipmentId, Guid IntentId, long Epoch, string Service,
    int CoverageVersion, string Phase, long GateVersion, Guid? ReadyProofId, Guid? CancelAcknowledgementId,
    int PendingCount, int OpenAggregateCount, int UnknownCount)
{
    public bool HasBlockers => PendingCount != 0 || OpenAggregateCount != 0 || UnknownCount != 0;
}

/// <summary>Called by authenticated source-side coordination, never directly from an Admin payload.</summary>
public interface ISourceRetirementGate
{
    Task<SourceRetirementObservation> PrepareAsync(Guid equipment, Guid intent, long epoch, CancellationToken ct);
    // Caller must first obtain the bound durable Equipment decision through authenticated gRPC.
    Task<SourceRetirementObservation> ObserveEquipmentDecisionAsync(Guid equipment, Guid intent, long epoch,
        string equipmentDecision, CancellationToken ct);
}
