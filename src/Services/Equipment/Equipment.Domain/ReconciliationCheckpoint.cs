namespace MedEquipSLA.Equipment.Domain;

// Persistence shape approved in ERD v0.1; use-case invariants belong in Domain/Application.
public sealed class ReconciliationCheckpoint
{
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
