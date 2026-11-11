namespace MedEquipSLA.Equipment.Domain;

// Persistence shape approved in ERD v0.1; use-case invariants belong in Domain/Application.
public sealed class EquipmentStateProjection
{
    public static EquipmentStateProjection ForWorkOrder(Guid aggregate, Guid equipment) => new()
    { SourceService = "Maintenance", SourceAggregateId = aggregate, EquipmentId = equipment };
    public void ApplyWorkOrder(string status, long version, Guid eventId, DateTimeOffset occurred, DateTimeOffset now)
    {
        if (status is not ("Scheduled" or "InProgress" or "Completed" or "Closed" or "Cancelled"))
            throw new InvalidOperationException("Invalid work order status.");
        SourceStatus = status; IsBlocking = status is "InProgress" or "Completed";
        SourceVersion = version; LastEventId = eventId; LastOccurredAtUtc = occurred; AppliedAtUtc = now;
    }
    public static EquipmentStateProjection ForIncident(Guid aggregate, Guid equipment) => new()
    {
        SourceService = "Incident", SourceAggregateId = aggregate, EquipmentId = equipment
    };

    public void ApplyIncident(string status, long version, Guid eventId, DateTimeOffset occurred, DateTimeOffset now)
    {
        SourceStatus = status;
        IsBlocking = status != "Closed";
        SourceVersion = version;
        LastEventId = eventId;
        LastOccurredAtUtc = occurred;
        AppliedAtUtc = now;
    }

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
