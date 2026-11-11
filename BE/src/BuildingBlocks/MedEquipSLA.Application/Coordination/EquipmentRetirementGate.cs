namespace MedEquipSLA.Application.Coordination;

/// <summary>Stored separately in each source DB; no cross-service EF relationship or timeout release.</summary>
public sealed class EquipmentRetirementGate
{
    private EquipmentRetirementGate() { }
    public static EquipmentRetirementGate Open(Guid equipment, DateTimeOffset now)
    {
        if (equipment == Guid.Empty) throw new ArgumentException("Equipment identity is required.");
        return new() { EquipmentId = equipment, CreatedAtUtc = now.ToUniversalTime(), UpdatedAtUtc = now.ToUniversalTime() };
    }

    public void AdmitNew(DateTimeOffset now)
    {
        if (Phase != "Open") throw new SourceAdmissionClosedException();
        Advance(now);
    }

    public bool Freeze(Guid intent, long epoch, DateTimeOffset now)
    {
        Validate(intent, epoch);
        if (epoch < HighestRetirementEpoch) return false;
        if (epoch == HighestRetirementEpoch)
        {
            if (CurrentIntentId != intent) throw new InvalidOperationException("Intent epoch collision.");
            return false; // Includes cancellation tombstone: delayed Prepare can never re-freeze.
        }
        if (Phase != "Open") throw new InvalidOperationException("Previous coordination is unresolved.");
        CurrentIntentId = intent;
        HighestRetirementEpoch = epoch;
        Phase = "Frozen";
        ReadyProofId = null;
        CancelAcknowledgementId = null;
        Advance(now);
        return true;
    }

    public bool Seal(bool sourceHasBlockers, DateTimeOffset now)
    {
        if (Phase == "Sealed") return false;
        if (Phase != "Frozen" || sourceHasBlockers)
            throw new InvalidOperationException("Only a frozen source with verified zero blockers can seal.");
        ReadyProofId = Guid.NewGuid();
        Phase = "Sealed";
        Advance(now);
        return true;
    }

    public bool AcknowledgeCancellation(Guid intent, long epoch, DateTimeOffset now)
    {
        Validate(intent, epoch);
        if (Phase == "Retired") throw new InvalidOperationException("Retirement is terminal.");
        if (epoch < HighestRetirementEpoch) return false;
        if (epoch == HighestRetirementEpoch && CurrentIntentId != intent)
            throw new InvalidOperationException("Intent epoch collision.");
        if (epoch > HighestRetirementEpoch && Phase != "Open")
            throw new InvalidOperationException("Previous coordination is unresolved.");
        if (epoch == HighestRetirementEpoch && Phase == "Open" && CancelAcknowledgementId is not null) return false;
        CurrentIntentId = intent;
        HighestRetirementEpoch = epoch;
        Phase = "Open";
        CancelAcknowledgementId = Guid.NewGuid();
        Advance(now);
        return true;
    }

    public bool ObserveCompleted(Guid intent, long epoch, DateTimeOffset now)
    {
        Validate(intent, epoch);
        if (CurrentIntentId != intent || HighestRetirementEpoch != epoch)
            throw new InvalidOperationException("Completed retirement binding mismatch.");
        if (Phase == "Retired") return false;
        if (Phase != "Sealed") throw new InvalidOperationException("Completion without a durable local seal is inconsistent.");
        Phase = "Retired";
        Advance(now);
        return true;
    }

    private static void Validate(Guid intent, long epoch)
    {
        if (intent == Guid.Empty || epoch <= 0) throw new InvalidOperationException("A positive bound retirement epoch is required.");
    }
    private void Advance(DateTimeOffset now)
    {
        SourceGateVersion = checked(SourceGateVersion + 1);
        // Versions order decisions. A backwards wall clock must not invalidate a durable gate.
        var utc = now.ToUniversalTime();
        if (utc > UpdatedAtUtc) UpdatedAtUtc = utc;
    }

    public Guid EquipmentId { get; private set; }
    public Guid? CurrentIntentId { get; private set; }
    public long HighestRetirementEpoch { get; private set; }
    public string Phase { get; private set; } = "Open";
    public long SourceGateVersion { get; private set; }
    public Guid? ReadyProofId { get; private set; }
    public Guid? CancelAcknowledgementId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
}

public sealed class SourceAdmissionClosedException() : InvalidOperationException("Equipment retirement admission is closed.");
