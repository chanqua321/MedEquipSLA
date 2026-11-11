namespace MedEquipSLA.Notification.Application.Abstractions;

public sealed record EmailDeliveryView(Guid Id, Guid NotificationId, string ToAddressSnapshot, string Subject, string Status,
    int AttemptCount, DateTimeOffset? NextAttemptAtUtc, DateTimeOffset? SentAtUtc, string? LastError, byte[] RowVersion);
public sealed record EmailAttemptView(int AttemptNumber, DateTimeOffset StartedAtUtc, DateTimeOffset? FinishedAtUtc,
    string Outcome, string? SmtpResponse, string? ErrorCode);
public interface IEmailDeliveryReader
{
    Task<IReadOnlyList<EmailDeliveryView>> ListAsync(string? status, int page, CancellationToken ct);
    Task<IReadOnlyList<EmailAttemptView>> AttemptsAsync(Guid id, CancellationToken ct);
}
