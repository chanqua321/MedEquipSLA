using MedEquipSLA.Contracts.Events;

namespace MedEquipSLA.Maintenance.Infrastructure.Persistence.Entities;

// Persistence shape approved in ERD v0.1; use-case invariants belong in Domain/Application.
public sealed class OutboxMessage
{
    private OutboxMessage() { }

    public OutboxMessage(IntegrationEvent message, string aggregateType = "MaintenanceSchedule")
    {
        EventId = message.EventId;
        EventType = message.EventType;
        SchemaVersion = message.SchemaVersion;
        AggregateType = aggregateType;
        AggregateId = message.AggregateId;
        AggregateVersion = message.AggregateVersion;
        OccurredAtUtc = message.OccurredAtUtc;
        CorrelationId = message.CorrelationId;
        PayloadJson = message.Payload;
        Status = "Pending";
    }

    public Guid EventId { get; private set; } = Guid.NewGuid();
    public string EventType { get; private set; } = string.Empty;
    public int SchemaVersion { get; private set; }
    public string AggregateType { get; private set; } = string.Empty;
    public Guid AggregateId { get; private set; }
    public long AggregateVersion { get; private set; } = 1;
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public string CorrelationId { get; private set; } = string.Empty;
    public string PayloadJson { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public int AttemptCount { get; private set; }
    public DateTimeOffset? NextAttemptAtUtc { get; private set; }
    public string? LeaseOwner { get; private set; }
    public DateTimeOffset? LeaseUntilUtc { get; private set; }
    public DateTimeOffset? PublishedAtUtc { get; private set; }
    public string? LastError { get; private set; }
}
