namespace MedEquipSLA.Notification.Domain;

// Persistence shape approved in ERD v0.1; use-case invariants belong in Domain/Application.
public sealed class NotificationRecord
{
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
