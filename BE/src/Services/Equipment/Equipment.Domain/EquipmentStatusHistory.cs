namespace MedEquipSLA.Equipment.Domain;

// Persistence shape approved in ERD v0.1; use-case invariants belong in Domain/Application.
public sealed class EquipmentStatusHistory
{
    internal static EquipmentStatusHistory Retirement(Guid equipment, string previous, EquipmentRetirementIntent intent,
        DateTimeOffset now) => new()
    {
        EquipmentId = equipment, ChangeKind = "Retired", FromStatus = previous, ToStatus = "Retired",
        ActorUserId = intent.ActorUserId, Reason = intent.Reason, ChangedAtUtc = now.ToUniversalTime()
        // SourceEventId stays null: no fictitious integration event.
    };

    public static EquipmentStatusHistory Change(Guid asset, string from, string to, Guid eventId, DateTimeOffset now) => new()
    {
        EquipmentId = asset, ChangeKind = "OperationalStatusChanged", FromStatus = from,
        ToStatus = to, SourceEventId = eventId, ChangedAtUtc = now
    };

    public static EquipmentStatusHistory Transfer(Guid asset, Department from, Department to, Guid actor, Guid eventId,
        string reason, DateTimeOffset now) => new()
    {
        EquipmentId = asset, ChangeKind = "DepartmentChanged", FromDepartmentId = from.Id, ToDepartmentId = to.Id,
        FromDepartmentNameSnapshot = from.Name, ToDepartmentNameSnapshot = to.Name, Reason = reason,
        ActorUserId = actor, SourceEventId = eventId, ChangedAtUtc = now
    };

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
