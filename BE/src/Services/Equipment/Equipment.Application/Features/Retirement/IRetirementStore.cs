using MedEquipSLA.Equipment.Domain;

namespace MedEquipSLA.Equipment.Application.Features.Retirement;

public sealed record RetirementReceiptView(string Source, string State, int CoverageVersion, long GateVersion,
    Guid? ReadyProofId, Guid? CancelAcknowledgementId, DateTimeOffset ReceivedAtUtc);
public sealed record RetirementView(Guid Id, Guid EquipmentId, long Epoch, string State, string Reason,
    byte[] RowVersion, IReadOnlyList<RetirementReceiptView> Receipts, int ContinueCount = 0);
public enum RetirementOutcome { Saved, NotFound, Conflict, Invalid }
public sealed record RetirementResult(RetirementOutcome Outcome, RetirementView? Value = null);
public sealed record RetirementCandidate(Guid EquipmentId, string Code, string Name, string Status, bool CoordinationRequired = true);

public interface IRetirementStore
{
    Task<RetirementResult> BeginAsync(Guid equipment, Guid actor, Guid key, string reason, string correlation, CancellationToken ct);
    Task<RetirementResult> AdminAsync(Guid equipment, Guid intent, Guid actor, Guid key, byte[] rowVersion,
        string action, string reason, string correlation, CancellationToken ct);
    Task<RetirementView?> FindAsync(Guid equipment, Guid intent, CancellationToken ct);
    Task<IReadOnlyList<RetirementView>> ListAsync(int page, CancellationToken ct);
    Task<IReadOnlyList<RetirementCandidate>> CandidatesAsync(string? search, int page, CancellationToken ct);
    Task<IReadOnlyList<RetirementView>> HistoryAsync(Guid equipment, int page, CancellationToken ct);
    Task<RetirementResult> ReportAsync(string source, Guid equipment, Guid intent, long epoch, int coverage, long version,
        string state, Guid? proof, string correlation, CancellationToken ct);
}

// Invoked only by owner persistence after Equipment row lock and local verification; Domain remains EF-independent.
public static class RetirementTransitions
{
    public static EquipmentRetirementIntent Begin(EquipmentAsset asset, IReadOnlyCollection<EquipmentRetirementIntent> history,
        Guid actor, Guid key, string reason, DateTimeOffset now) =>
        EquipmentRetirementIntent.Begin(asset, history, actor, key, reason, now);

    public static EquipmentStatusHistory Complete(EquipmentAsset asset, EquipmentRetirementIntent intent,
        bool unresolvedRegistrations, bool blockingProjection, bool unknownLegacy, DateTimeOffset now)
    {
        var eligibility = VerifiedRetirementEligibility.Create(intent, true,
            unresolvedRegistrations, blockingProjection, unknownLegacy);
        var previous = asset.OperationalStatus;
        asset.Retire(intent, eligibility, now);
        return EquipmentStatusHistory.Retirement(asset.Id, previous, intent, now);
    }
}
