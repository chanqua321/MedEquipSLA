namespace MedEquipSLA.Notification.Infrastructure.Persistence.Entities;

// Persistence shape approved in ERD v0.1; use-case invariants belong in Domain/Application.
public sealed class InboxMessage
{
    public string ConsumerName { get; private set; } = string.Empty;
    public Guid EventId { get; private set; } = Guid.NewGuid();
    public string EventType { get; private set; } = string.Empty;
    public int SchemaVersion { get; private set; }
    public DateTimeOffset ProcessedAtUtc { get; private set; }
}
