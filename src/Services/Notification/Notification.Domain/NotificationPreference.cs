namespace MedEquipSLA.Notification.Domain;

// Persistence shape approved in ERD v0.1; use-case invariants belong in Domain/Application.
public sealed class NotificationPreference
{
    public Guid UserId { get; private set; }
    public string NotificationKind { get; private set; } = string.Empty;
    public bool InAppEnabled { get; private set; }
    public bool EmailEnabled { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
}
