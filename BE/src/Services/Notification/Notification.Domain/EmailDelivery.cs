namespace MedEquipSLA.Notification.Domain;

// Persistence shape approved in ERD v0.1; use-case invariants belong in Domain/Application.
public sealed class EmailDelivery
{
    public static EmailDelivery Queue(NotificationRecord notification, string? address, DateTimeOffset now)
    {
        var row = new EmailDelivery { NotificationId = notification.Id, ToAddressSnapshot = address ?? "", Subject = notification.Title,
            Body = notification.Body, Status = address is null ? "Failed" : "Pending", CreatedAtUtc = now.ToUniversalTime(),
            LastError = address is null ? "RecipientUnavailable" : null };
        row.MessageId = $"<{row.Id:N}@medequip.local>";
        return row;
    }
    public void Claim(string owner, DateTimeOffset now)
    {
        if (Status is not ("Pending" or "Processing") || LeaseUntilUtc > now || NextAttemptAtUtc > now || AttemptCount >= 8)
            throw new InvalidOperationException("Email is not eligible for sending.");
        Status = "Processing"; LeaseOwner = owner; LeaseUntilUtc = now.AddSeconds(30); AttemptCount++;
    }
    public bool Owned(string owner) => Status == "Processing" && LeaseOwner == owner;
    public void Suppress(string reason) { Status = "Failed"; LastError = reason; }
    public void Exhaust(string reason)
    { Status = "Failed"; LastError = reason; LeaseOwner = null; LeaseUntilUtc = null; NextAttemptAtUtc = null; }
    public void Finish(bool sent, bool permanent, string? error, DateTimeOffset now)
    {
        if (Status != "Processing") throw new InvalidOperationException("An email send attempt is required.");
        Status = sent ? "Sent" : permanent || AttemptCount >= 8 ? "Failed" : "Pending";
        SentAtUtc = sent ? now.ToUniversalTime() : null; LastError = error;
        NextAttemptAtUtc = Status == "Pending" ? now.AddSeconds(Math.Min(300, 5 * (1 << Math.Min(AttemptCount, 6)))) : null;
        LeaseOwner = null; LeaseUntilUtc = null;
    }
    public void Retry(string? currentAddress, DateTimeOffset now)
    {
        if (Status != "Failed" || currentAddress is null) throw new InvalidOperationException("Failed delivery and active recipient required.");
        ToAddressSnapshot = currentAddress; Status = "Pending"; AttemptCount = 0; NextAttemptAtUtc = now;
        LastError = null; LeaseOwner = null; LeaseUntilUtc = null;
    }
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
