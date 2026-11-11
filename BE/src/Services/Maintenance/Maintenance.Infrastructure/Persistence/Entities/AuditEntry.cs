namespace MedEquipSLA.Maintenance.Infrastructure.Persistence.Entities;

// Persistence shape approved in ERD v0.1; use-case invariants belong in Domain/Application.
public sealed class AuditEntry
{
    public static AuditEntry ForScheduleCommand(Guid schedule, string action, Guid? actor, DateTimeOffset now,
        string correlation, string changes, Guid? audit = null) => new()
    { Id = audit ?? Guid.NewGuid(), ActorUserId = actor, ActorService = actor is null ? "Maintenance" : null,
        EntityType = "MaintenanceSchedule", EntityId = schedule.ToString(), Action = action, OccurredAtUtc = now.ToUniversalTime(),
        CorrelationId = correlation, ChangesJson = changes };
    public static AuditEntry ForCreationOperation(Guid operation, string action, DateTimeOffset now, string correlation,
        string changes, Guid? actor = null, Guid? audit = null) => new()
    { Id = audit ?? Guid.NewGuid(), ActorUserId = actor, ActorService = actor is null ? "Maintenance" : null,
        EntityType = "MaintenanceCreationOperation", EntityId = operation.ToString(), Action = action,
        OccurredAtUtc = now.ToUniversalTime(), CorrelationId = correlation, ChangesJson = changes };
    private AuditEntry() { }

    public AuditEntry(Guid actor, string action, Guid schedule, DateTimeOffset now, string trace, string changes)
    {
        ActorUserId = actor;
        Action = action;
        EntityType = "MaintenanceSchedule";
        EntityId = schedule.ToString();
        OccurredAtUtc = now;
        CorrelationId = trace;
        ChangesJson = changes;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public static AuditEntry ForOperation(Guid operation, string action, DateTimeOffset now, string correlation, string changes, Guid? actor = null, Guid? auditId = null) =>
        new() { Id = auditId ?? Guid.NewGuid(), ActorService = actor is null ? "Maintenance" : null, ActorUserId = actor, Action = action,
            EntityType = "WorkOrderD3Operation", EntityId = operation.ToString(), OccurredAtUtc = now.ToUniversalTime(),
            CorrelationId = correlation, ChangesJson = changes };
    public static AuditEntry ForService(string action, Guid equipment, DateTimeOffset now, string correlation, string changes) =>
        new() { ActorService = "Maintenance", Action = action, EntityType = "EquipmentRetirementGate",
            EntityId = equipment.ToString(), OccurredAtUtc = now.ToUniversalTime(), CorrelationId = correlation, ChangesJson = changes };
    public Guid? ActorUserId { get; private set; }
    public string? ActorService { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string EntityType { get; private set; } = string.Empty;
    public string EntityId { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public string CorrelationId { get; private set; } = string.Empty;
    public string? ChangesJson { get; private set; }
}
