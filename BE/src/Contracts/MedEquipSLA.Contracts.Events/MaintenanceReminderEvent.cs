namespace MedEquipSLA.Contracts.Events;

public sealed record MaintenanceReminderEvent(Guid ScheduleId, Guid EquipmentId, string EquipmentCode, string EquipmentName,
    Guid DepartmentId, DateTimeOffset ScheduledOccurrenceAtUtc, DateTimeOffset DueAtUtc,
    DateTimeOffset? ReminderWindowStartAtUtc, string ReminderKind, Guid? AssignedTechnicianId, Guid[] RecipientUserIds)
{
    public const string Stream = "medequip:maintenance:reminders:v1";
    public const string DueSoon = "MaintenanceDueSoon";
    public const string Overdue = "MaintenanceOverdue";
}

public sealed record NotificationRecipientRequest(Guid? TechnicianId);
public sealed record NotificationRecipientReply(Guid[] UserIds);

public interface INotificationRecipientLookup
{
    Task<Guid[]> ResolveAsync(Guid? technicianId, CancellationToken ct);
}
