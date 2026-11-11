namespace MedEquipSLA.Notification.Domain;

// Persistence shape approved in ERD v0.1; use-case invariants belong in Domain/Application.
public sealed class EmailDeliveryAttempt
{
    public static EmailDeliveryAttempt Start(Guid delivery, int number, DateTimeOffset now) => new()
    { EmailDeliveryId = delivery, AttemptNumber = number, StartedAtUtc = now.ToUniversalTime(), Outcome = "Processing" };
    public void Finish(string outcome, string? error, DateTimeOffset now)
    {
        if (Outcome != "Processing") return;
        Outcome = outcome; ErrorCode = error; FinishedAtUtc = now.ToUniversalTime();
        SmtpResponse = outcome == "Sent" ? "SMTP server accepted the message." : null;
    }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid EmailDeliveryId { get; private set; }
    public int AttemptNumber { get; private set; }
    public DateTimeOffset StartedAtUtc { get; private set; }
    public DateTimeOffset? FinishedAtUtc { get; private set; }
    public string Outcome { get; private set; } = string.Empty;
    public string? SmtpResponse { get; private set; }
    public string? ErrorCode { get; private set; }
}
