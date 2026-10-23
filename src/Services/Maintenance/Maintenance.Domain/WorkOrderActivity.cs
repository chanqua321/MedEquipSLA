namespace MedEquipSLA.Maintenance.Domain;

// Persistence shape approved in ERD v0.1; use-case invariants belong in Domain/Application.
public sealed class WorkOrderActivity
{
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
