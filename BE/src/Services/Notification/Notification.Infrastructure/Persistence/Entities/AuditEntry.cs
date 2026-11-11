namespace MedEquipSLA.Notification.Infrastructure.Persistence.Entities;

// Persistence shape approved in ERD v0.1; use-case invariants belong in Domain/Application.
public sealed class AuditEntry
{
    public static AuditEntry Record(string entityType, Guid entity, string action, DateTimeOffset now, string changes, Guid? actor = null) =>
        new() { EntityType = entityType, EntityId = entity.ToString(), Action = action, OccurredAtUtc = now.ToUniversalTime(),
            ChangesJson = changes, ActorUserId = actor, ActorService = actor is null ? "Notification" : null, CorrelationId = entity.ToString() };
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid? ActorUserId { get; private set; }
    public string? ActorService { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string EntityType { get; private set; } = string.Empty;
    public string EntityId { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public string CorrelationId { get; private set; } = string.Empty;
    public string? ChangesJson { get; private set; }
}
