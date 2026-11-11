namespace MedEquipSLA.Equipment.Domain;

// Persistence shape approved in ERD v0.1; use-case invariants belong in Domain/Application.
public sealed class ReconciliationCheckpoint
{
    public static ReconciliationCheckpoint ForSource(string source) => source is "Incident" or "Maintenance"
        ? new() { SourceService = source, State = "Idle" } : throw new ArgumentException("Unknown source.");
    public bool Admit(Guid snapshot, string watermark, DateTimeOffset captured, int index)
    {
        if (SnapshotId == snapshot)
        {
            if (SnapshotWatermark != watermark) throw new InvalidOperationException("Snapshot binding mismatch.");
            var expected = int.Parse((Cursor ?? "0:0").Split(':')[0], System.Globalization.CultureInfo.InvariantCulture);
            if (index < expected) return false;
            if (State == "Failed") return false;
            if (State != "Running" || index != expected) throw new SnapshotPageNotReadyException();
            return true;
        }
        if (StartedAtUtc is { } prior && captured <= prior) return false;
        if (State == "Running" || index != 0) throw new SnapshotPageNotReadyException();
        SnapshotId = snapshot; SnapshotWatermark = watermark; StartedAtUtc = captured;
        Cursor = "0:0"; State = "Running"; LastError = null; return true;
    }
    public void Advance(int index, int pages, int items, int totalItems, DateTimeOffset now, string? unresolved)
    {
        var parts = Cursor!.Split(':');
        if (State != "Running" || int.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture) != index)
            throw new InvalidOperationException("Snapshot cursor mismatch.");
        var seen = checked(int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture)+items);
        if (seen > totalItems || index+1 == pages && seen != totalItems) throw new InvalidOperationException("Snapshot coverage mismatch.");
        Cursor = $"{index+1}:{seen}";
        if (unresolved is not null) LastError = unresolved;
        if (index+1 == pages) { State = "Completed"; LastCompletedAtUtc = now.ToUniversalTime(); }
    }
    public void RecordFailure(Guid snapshot, string watermark, DateTimeOffset captured, string error, bool exhausted)
    {
        if (SnapshotId is not null && SnapshotId != snapshot) return;
        if (SnapshotId == snapshot && SnapshotWatermark != watermark) return;
        SnapshotId = snapshot; SnapshotWatermark = watermark; StartedAtUtc ??= captured;
        Cursor ??= "0:0"; LastError = error;
        State = exhausted ? "Failed" : State == "Idle" ? "Running" : State;
    }
    public string SourceService { get; private set; } = string.Empty;
    public Guid? SnapshotId { get; private set; }
    public string? SnapshotWatermark { get; private set; }
    public string? Cursor { get; private set; }
    public string State { get; private set; } = string.Empty;
    public DateTimeOffset? StartedAtUtc { get; private set; }
    public DateTimeOffset? LastCompletedAtUtc { get; private set; }
    public string? LastError { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
}

public sealed class SnapshotPageNotReadyException() : InvalidOperationException("Prior snapshot pages are still required.");
