namespace MedEquipSLA.Equipment.Infrastructure.Persistence.Entities;

// Persistence shape approved in ERD v0.1; use-case invariants belong in Domain/Application.
public sealed class AuditEntry
{
    private AuditEntry() { }

    public AuditEntry(Guid? actor, string action, string entityType, Guid entityId,
        DateTimeOffset occurredAt, string correlationId, string changes)
    {
        ActorUserId = actor;
        Action = action;
        EntityType = entityType;
        EntityId = entityId.ToString();
        OccurredAtUtc = occurredAt;
        CorrelationId = correlationId;
        ChangesJson = changes;
    }

    public static AuditEntry ForService(string service, string action, string entityType, Guid entityId,
        DateTimeOffset occurredAt, string correlationId, string changes)
    {
        if (service is not ("Equipment" or "Maintenance" or "Incident"))
            throw new ArgumentException("An identified equipment-protocol service actor is required.", nameof(service));
        return new AuditEntry(null, action, entityType, entityId, occurredAt.ToUniversalTime(), correlationId, changes)
        { ActorService = service };
    }

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
