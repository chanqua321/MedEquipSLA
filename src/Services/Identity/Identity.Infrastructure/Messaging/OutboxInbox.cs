namespace MedEquipSLA.Identity.Infrastructure.Messaging;
// Schema and transaction behavior are pending BR review. These types are not yet mapped.
public sealed record OutboxMessage(Guid EventId, string Payload, DateTimeOffset OccurredAtUtc);
public sealed record InboxMessage(string ConsumerName, Guid EventId, DateTimeOffset ProcessedAtUtc);
