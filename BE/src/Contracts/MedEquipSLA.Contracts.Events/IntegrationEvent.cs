namespace MedEquipSLA.Contracts.Events;
public sealed record IntegrationEvent(Guid EventId, string EventType, int SchemaVersion,
    string Producer, Guid AggregateId, long AggregateVersion, DateTimeOffset OccurredAtUtc,
    string CorrelationId, string Payload);
public interface IEventPublisher
{ Task PublishAsync(string stream, IntegrationEvent message, CancellationToken cancellationToken); }
public interface IEventConsumer
{ Task ConsumeAsync(IntegrationEvent message, CancellationToken cancellationToken); }
