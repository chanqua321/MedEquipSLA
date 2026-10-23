namespace MedEquipSLA.Equipment.Infrastructure.Persistence.Entities;

// Persistence shape approved in ERD v0.1; use-case invariants belong in Domain/Application.
public sealed class AuditEntry
{
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
