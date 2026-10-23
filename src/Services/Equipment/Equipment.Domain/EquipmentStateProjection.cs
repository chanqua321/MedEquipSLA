namespace MedEquipSLA.Equipment.Domain;

// Persistence shape approved in ERD v0.1; use-case invariants belong in Domain/Application.
public sealed class EquipmentStateProjection
{
    public string SourceService { get; private set; } = string.Empty;
    public Guid SourceAggregateId { get; private set; }
    public Guid EquipmentId { get; private set; }
    public string SourceStatus { get; private set; } = string.Empty;
    public bool IsBlocking { get; private set; }
    public long SourceVersion { get; private set; }
    public Guid LastEventId { get; private set; }
    public DateTimeOffset LastOccurredAtUtc { get; private set; }
    public DateTimeOffset AppliedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
}
