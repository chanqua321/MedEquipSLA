namespace MedEquipSLA.Incident.Infrastructure.Persistence.Entities;

// Persistence shape approved in ERD v0.1; use-case invariants belong in Domain/Application.
public sealed class AuditEntry
{
    public static AuditEntry ForCreationOperation(Guid operation, string action, DateTimeOffset now, string correlation,
        string changes, Guid? actor = null, Guid? audit = null) => new()
    { Id = audit ?? Guid.NewGuid(), ActorUserId = actor, ActorService = actor is null ? "Incident" : null,
        EntityType = "IncidentCreationOperation", EntityId = operation.ToString(), Action = action,
        OccurredAtUtc = now.ToUniversalTime(), CorrelationId = correlation, ChangesJson = changes };
    public static AuditEntry ForIncident(Guid actor, Guid incident, string action, DateTimeOffset now, string correlation, string changes) =>
        new() { ActorUserId = actor, Action = action, EntityType = "Incident", EntityId = incident.ToString(),
            OccurredAtUtc = now.ToUniversalTime(), CorrelationId = correlation, ChangesJson = changes };
    public static AuditEntry ForService(string action, Guid equipment, DateTimeOffset now, string correlation, string changes) =>
        new() { ActorService = "Incident", Action = action, EntityType = "EquipmentRetirementGate",
            EntityId = equipment.ToString(), OccurredAtUtc = now.ToUniversalTime(), CorrelationId = correlation, ChangesJson = changes };
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
