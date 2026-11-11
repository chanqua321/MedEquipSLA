using System.Security.Cryptography;
using System.Text.Json;

namespace MedEquipSLA.Equipment.Domain;

/// <summary>Durable coordination, separate from equipment operational status. No timeout transition.</summary>
public sealed class EquipmentRetirementIntent
{
    private readonly List<RetirementSourceReceipt> receipts = [];
    private EquipmentRetirementIntent() { }

    // Application must load complete history and allocate under the Equipment SQL row lock.
    internal static EquipmentRetirementIntent Begin(EquipmentAsset equipment,
        IReadOnlyCollection<EquipmentRetirementIntent> history, Guid actor, Guid key, string reason, DateTimeOffset now)
    {
        reason = RetirementRules.Reason(reason);
        if (actor == Guid.Empty || key == Guid.Empty || equipment.IsRetired)
            throw new InvalidOperationException("A non-retired equipment and identified Admin command are required.");
        if (history.Any(x => x.EquipmentId != equipment.Id || x.IsActive || x.State == "Completed"))
            throw new InvalidOperationException("History is incomplete, inconsistent or has an active intent.");
        var epoch = checked(history.Select(x => x.RetirementEpoch).DefaultIfEmpty(0).Max() + 1);
        return new()
        {
            EquipmentId = equipment.Id, RetirementEpoch = epoch, ActorUserId = actor,
            RequestKey = key, Reason = reason, State = "Coordinating",
            RequestHash = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { equipment.Id, actor, reason })),
            CreatedAtUtc = now.ToUniversalTime(), UpdatedAtUtc = now.ToUniversalTime()
        };
    }

    public void Continue(Guid actor, string reason, DateTimeOffset now)
    {
        Require(IsActive, "Only an unresolved retirement can continue.");
        if (actor == Guid.Empty) throw new ArgumentException("Actor is required.", nameof(actor));
        LastContinueReason = RetirementRules.Reason(reason);
        LastContinuedByUserId = actor;
        LastContinuedAtUtc = now.ToUniversalTime();
        ContinueCount = checked(ContinueCount + 1);
        UpdatedAtUtc = now.ToUniversalTime();
    }

    public bool RecordSource(string authenticatedSource, string source, Guid equipmentId, Guid intentId, long epoch,
        int coverageVersion, long gateVersion, string sourceState, Guid? proofId, DateTimeOffset now)
    {
        Require(IsActive, "Terminal retirement cannot receive new reports.");
        Require(authenticatedSource == source && source is "Maintenance" or "Incident", "Source identity mismatch.");
        Require(equipmentId == EquipmentId && intentId == Id && epoch == RetirementEpoch, "Intent binding mismatch.");
        Require(coverageVersion == 1, "Required coverage is Maintenance WO+Schedule v1 and Incident v1.");
        Require(sourceState != "CancelAck" || State == "Cancelling", "CancelAck requires a durable cancelling decision.");
        Require(State != "Cancelling" || sourceState == "CancelAck", "Cancelling only accepts cancellation acknowledgments.");
        var existing = receipts.SingleOrDefault(x => x.SourceService == source);
        if (existing is null)
        {
            existing = RetirementSourceReceipt.Create(EquipmentId, Id, RetirementEpoch, source, coverageVersion,
                gateVersion, sourceState, proofId, now);
            receipts.Add(existing);
        }
        else if (!existing.Apply(gateVersion, sourceState, proofId, now)) return false;
        UpdatedAtUtc = now.ToUniversalTime();
        if (State == "Coordinating" && HasAll("Sealed")) State = "Ready";
        return true;
    }

    public void RequestCancel(Guid actor, string reason, DateTimeOffset now)
    {
        Require(State is "Coordinating" or "Ready", "Only unresolved retirement can be cancelled.");
        if (actor == Guid.Empty) throw new ArgumentException("Actor is required.", nameof(actor));
        CancelReason = RetirementRules.Reason(reason);
        CancelRequestedByUserId = actor;
        CancelRequestedAtUtc = now.ToUniversalTime();
        State = "Cancelling";
        UpdatedAtUtc = now.ToUniversalTime();
    }

    public void ConfirmCancelled(DateTimeOffset now)
    {
        Require(State == "Cancelling" && HasAll("CancelAck"), "All required source cancellation acknowledgments are required.");
        State = "Cancelled";
        CancelledAtUtc = now.ToUniversalTime();
        UpdatedAtUtc = now.ToUniversalTime();
    }

    internal void Complete(DateTimeOffset now)
    {
        Require(State == "Ready" && HasAll("Sealed"), "All required sealed sources are required.");
        State = "Completed";
        CompletedAtUtc = now.ToUniversalTime();
        UpdatedAtUtc = now.ToUniversalTime();
    }

    internal bool HasAll(string state) => new[] { "Maintenance", "Incident" }.All(source =>
        receipts.Any(x => x.SourceService == source && x.CoverageVersion == 1 && x.SourceState == state &&
            (state == "Sealed" ? x.ReadyProofId != null : x.CancelAcknowledgementId != null)));

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid EquipmentId { get; private set; }
    public long RetirementEpoch { get; private set; }
    public string Reason { get; private set; } = "";
    public Guid ActorUserId { get; private set; }
    public Guid RequestKey { get; private set; }
    private byte[] requestHash = [];
    public byte[] RequestHash { get => requestHash.ToArray(); private set => requestHash = value.ToArray(); }
    public string RequiredSourcesJson { get; private set; } = "{\"Maintenance\":1,\"Incident\":1}";
    public string State { get; private set; } = "Coordinating";
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public Guid? CancelRequestedByUserId { get; private set; }
    public string? CancelReason { get; private set; }
    public DateTimeOffset? CancelRequestedAtUtc { get; private set; }
    public DateTimeOffset? CancelledAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public int ContinueCount { get; private set; }
    public Guid? LastContinuedByUserId { get; private set; }
    public string? LastContinueReason { get; private set; }
    public DateTimeOffset? LastContinuedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public IReadOnlyCollection<RetirementSourceReceipt> Receipts => receipts.AsReadOnly();
    public bool IsActive => State is "Coordinating" or "Ready" or "Cancelling";
}

internal static class RetirementRules
{
    internal static string Reason(string value)
    {
        var result = value?.Trim();
        if (result is not { Length: > 0 and <= 2000 }) throw new ArgumentException("Reason must contain 1..2000 characters.");
        return result;
    }
}

/// <summary>Only Application (friend assembly) may attest verified local eligibility, under SQL locks.</summary>
internal sealed class VerifiedRetirementEligibility
{
    private VerifiedRetirementEligibility(Guid equipment, Guid intent, long epoch)
    { EquipmentId = equipment; IntentId = intent; Epoch = epoch; }

    internal static VerifiedRetirementEligibility Create(EquipmentRetirementIntent intent,
        bool localCoordinationVerified, bool unresolvedRegistrations, bool blockingProjection, bool unknownLegacy)
    {
        if (!localCoordinationVerified || unresolvedRegistrations || blockingProjection || unknownLegacy ||
            intent.State != "Ready" || !intent.HasAll("Sealed"))
            throw new InvalidOperationException("Retirement coordination is unresolved.");
        return new(intent.EquipmentId, intent.Id, intent.RetirementEpoch);
    }
    internal Guid EquipmentId { get; }
    internal Guid IntentId { get; }
    internal long Epoch { get; }
}
