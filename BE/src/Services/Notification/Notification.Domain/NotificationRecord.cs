namespace MedEquipSLA.Notification.Domain;

// Persistence shape approved in ERD v0.1; use-case invariants belong in Domain/Application.
public sealed class NotificationRecord
{
    public bool IsInAppVisible { get; private set; } = true;
    public bool IsEmailRequested { get; private set; } = true;
    public DateTimeOffset? RealtimeNotifiedAtUtc { get; private set; }
    public void SetInAppVisibility(bool visible) => IsInAppVisible = visible;
    public void SetChannels(bool inApp, bool email) { IsInAppVisible = inApp; IsEmailRequested = email; }
    public static NotificationRecord UserEvent(Guid recipient, Guid source, string kind, string resourceType, Guid resource,
        Guid? department, string title, string body, DateTimeOffset now) => new()
    { RecipientUserId = recipient, SourceEventId = source, Kind = kind, ResourceType = resourceType, ResourceId = resource,
        DepartmentIdSnapshot = department, Title = title, Body = body, CreatedAtUtc = now.ToUniversalTime() };
    private NotificationRecord() { }

    public NotificationRecord(Guid recipientId, Guid eventId, string kind, Guid incidentId,
        Guid departmentId, string deadline, DateTimeOffset now)
    {
        RecipientUserId = recipientId;
        SourceEventId = eventId;
        Kind = kind;
        Title = "Cảnh báo SLA tạm tính";
        Body = $"Đã quá hạn SLA ({deadline}). Kết quả cuối được xác định khi đóng sự cố.";
        ResourceType = "Incident";
        ResourceId = incidentId;
        DepartmentIdSnapshot = departmentId;
        CreatedAtUtc = now;
    }

    public static NotificationRecord Reminder(Guid recipient, Guid eventId, string kind, Guid occurrence,
        string equipmentCode, string equipmentName, Guid department, string reminderKind,
        DateTimeOffset scheduled, DateTimeOffset due, DateTimeOffset now) => new()
    {
        RecipientUserId = recipient, SourceEventId = eventId, Kind = kind,
        Title = reminderKind == "DueSoon" ? "Sắp đến kỳ bảo trì" : "Quá hạn kỳ bảo trì",
        Body = $"Thiết bị {equipmentCode} - {equipmentName}. Kỳ dự kiến {scheduled:O}, hạn hoàn thành {due:O}.",
        ResourceType = "ScheduleOccurrence", ResourceId = occurrence, DepartmentIdSnapshot = department, CreatedAtUtc = now
    };

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid RecipientUserId { get; private set; }
    public Guid SourceEventId { get; private set; }
    public string Kind { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public string ResourceType { get; private set; } = string.Empty;
    public Guid ResourceId { get; private set; }
    public Guid? DepartmentIdSnapshot { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? ReadAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
}
