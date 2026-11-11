namespace MedEquipSLA.Maintenance.Domain;

// Persistence shape approved in ERD v0.1; use-case invariants belong in Domain/Application.
public sealed class WorkOrderActivity
{
    public static WorkOrderActivity Transition(Guid order, string action, string before, string after, Guid actor, string? reason,
        DateTimeOffset now) => new()
    { WorkOrderId = order, Action = action, FromStatus = before, ToStatus = after, ActorUserId = actor, Reason = reason, OccurredAtUtc = now };
    public static WorkOrderActivity Assignment(Guid id, string status, Guid? previous, Guid next, Guid actor,
        DateTimeOffset now) => new()
    {
        WorkOrderId = id, Action = "Assigned", FromStatus = status, ToStatus = status,
        FromTechnicianId = previous, ToTechnicianId = next, ActorUserId = actor, OccurredAtUtc = now
    };

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid WorkOrderId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string? FromStatus { get; private set; }
    public string? ToStatus { get; private set; }
    public Guid? FromTechnicianId { get; private set; }
    public Guid? ToTechnicianId { get; private set; }
    public Guid ActorUserId { get; private set; }
    public string? Reason { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; }
}
