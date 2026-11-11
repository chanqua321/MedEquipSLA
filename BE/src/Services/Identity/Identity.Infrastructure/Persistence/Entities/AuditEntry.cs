namespace MedEquipSLA.Identity.Infrastructure.Persistence.Entities;

// Persistence shape approved in ERD v0.1; use-case invariants belong in Domain/Application.
public sealed class AuditEntry
{
    public static AuditEntry ForSecurityService(Guid target, string action, string changes) => new()
    {
        ActorService = "Identity", EntityId = target.ToString(), EntityType = "User", Action = action,
        OccurredAtUtc = DateTimeOffset.UtcNow, CorrelationId = Guid.NewGuid().ToString(), ChangesJson = changes
    };
    private AuditEntry() { }

    public AuditEntry(Guid actor, Guid target, string action, string correlationId, string changes)
    {
        ActorUserId = actor;
        EntityId = target.ToString();
        EntityType = "User";
        Action = action;
        OccurredAtUtc = DateTimeOffset.UtcNow;
        CorrelationId = correlationId;
        ChangesJson = changes;
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
