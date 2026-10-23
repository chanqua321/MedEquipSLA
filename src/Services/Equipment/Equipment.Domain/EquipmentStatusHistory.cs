namespace MedEquipSLA.Equipment.Domain;

// Persistence shape approved in ERD v0.1; use-case invariants belong in Domain/Application.
public sealed class EquipmentStatusHistory
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid EquipmentId { get; private set; }
    public string ChangeKind { get; private set; } = string.Empty;
    public string? FromStatus { get; private set; }
    public string? ToStatus { get; private set; }
    public Guid? FromDepartmentId { get; private set; }
    public Guid? ToDepartmentId { get; private set; }
    public string? FromDepartmentNameSnapshot { get; private set; }
    public string? ToDepartmentNameSnapshot { get; private set; }
    public string? Reason { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public Guid? SourceEventId { get; private set; }
    public DateTimeOffset ChangedAtUtc { get; private set; }
}
