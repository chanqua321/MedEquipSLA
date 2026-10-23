namespace MedEquipSLA.Notification.Domain;

// Persistence shape approved in ERD v0.1; use-case invariants belong in Domain/Application.
public sealed class EmailDelivery
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid NotificationId { get; private set; }
    public string ToAddressSnapshot { get; private set; } = string.Empty;
    public string Subject { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public string MessageId { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public int AttemptCount { get; private set; }
    public DateTimeOffset? NextAttemptAtUtc { get; private set; }
    public string? LeaseOwner { get; private set; }
    public DateTimeOffset? LeaseUntilUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? SentAtUtc { get; private set; }
    public string? LastError { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
}
