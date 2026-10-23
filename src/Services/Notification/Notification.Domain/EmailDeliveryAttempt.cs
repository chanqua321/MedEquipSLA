namespace MedEquipSLA.Notification.Domain;

// Persistence shape approved in ERD v0.1; use-case invariants belong in Domain/Application.
public sealed class EmailDeliveryAttempt
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid EmailDeliveryId { get; private set; }
    public int AttemptNumber { get; private set; }
    public DateTimeOffset StartedAtUtc { get; private set; }
    public DateTimeOffset? FinishedAtUtc { get; private set; }
    public string Outcome { get; private set; } = string.Empty;
    public string? SmtpResponse { get; private set; }
    public string? ErrorCode { get; private set; }
}
