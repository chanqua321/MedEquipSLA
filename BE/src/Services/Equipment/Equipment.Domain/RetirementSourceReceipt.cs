namespace MedEquipSLA.Equipment.Domain;

/// <summary>Bound source assertion. Transport authentication is deliberately not implemented here.</summary>
public sealed class RetirementSourceReceipt
{
    private RetirementSourceReceipt() { }

    internal static RetirementSourceReceipt Create(Guid equipment, Guid intent, long epoch, string source,
        int coverage, long version, string state, Guid? proof, DateTimeOffset now)
    {
        var receipt = new RetirementSourceReceipt
        {
            EquipmentId = equipment, IntentId = intent, RetirementEpoch = epoch,
            SourceService = source, CoverageVersion = coverage
        };
        receipt.Apply(version, state, proof, now);
        return receipt;
    }

    internal bool Apply(long version, string state, Guid? proof, DateTimeOffset now)
    {
        if (version <= 0 || state is not ("Frozen" or "Blocked" or "Sealed" or "CancelAck"))
            throw new InvalidOperationException("Invalid source report.");
        if ((state is "Sealed" or "CancelAck") != (proof is not null && proof != Guid.Empty))
            throw new InvalidOperationException("A sealed or cancellation proof is required only for its matching state.");
        if (proof == Guid.Empty) throw new InvalidOperationException("Empty proof is invalid.");
        if (version < SourceGateVersion) throw new InvalidOperationException("Stale source report.");
        var currentProof = SourceState == "CancelAck" ? CancelAcknowledgementId : ReadyProofId;
        if (version == SourceGateVersion)
        {
            if (state == SourceState && proof == currentProof) return false;
            throw new InvalidOperationException("A version cannot identify different reports.");
        }
        if (SourceState == "CancelAck" || SourceState == "Sealed" && state != "CancelAck")
            throw new InvalidOperationException("Durable proof cannot be replaced by a later conflicting report.");
        SourceGateVersion = version;
        SourceState = state;
        if (state == "Sealed") ReadyProofId = proof;
        if (state == "CancelAck") CancelAcknowledgementId = proof;
        ReceivedAtUtc = now.ToUniversalTime();
        return true;
    }

    public Guid IntentId { get; private set; }
    public Guid EquipmentId { get; private set; }
    public long RetirementEpoch { get; private set; }
    public string SourceService { get; private set; } = "";
    public int CoverageVersion { get; private set; }
    public long SourceGateVersion { get; private set; }
    public Guid? ReadyProofId { get; private set; }
    public Guid? CancelAcknowledgementId { get; private set; }
    public string SourceState { get; private set; } = "";
    public DateTimeOffset ReceivedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
}
