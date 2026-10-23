namespace MedEquipSLA.Maintenance.Domain;

// Persistence shape approved in ERD v0.1; use-case invariants belong in Domain/Application.
public sealed class WorkOrder
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Code { get; private set; } = string.Empty;
    public Guid? OccurrenceId { get; private set; }
    public Guid EquipmentId { get; private set; }
    public string EquipmentCodeSnapshot { get; private set; } = string.Empty;
    public string EquipmentNameSnapshot { get; private set; } = string.Empty;
    public Guid DepartmentIdSnapshot { get; private set; }
    public string DepartmentNameSnapshot { get; private set; } = string.Empty;
    public string WorkType { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public Guid? AssignedTechnicianId { get; private set; }
    public DateTimeOffset ScheduledForUtc { get; private set; }
    public DateTimeOffset DueAtUtc { get; private set; }
    public DateTimeOffset? StartedAtUtc { get; private set; }
    public DateTimeOffset? ClosedAtUtc { get; private set; }
    public DateTimeOffset? CancelledAtUtc { get; private set; }
    public string? CancellationReason { get; private set; }
    public bool IsBlocking { get; private set; }
    public Guid EquipmentUseRegistrationId { get; private set; }
    public long AggregateVersion { get; private set; } = 1;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    public Guid? UpdatedByUserId { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
}
