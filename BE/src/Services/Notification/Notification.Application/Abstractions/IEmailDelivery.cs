namespace MedEquipSLA.Notification.Application.Abstractions;

public sealed record OutgoingEmail(string Address, string Subject, string Body, string MessageId);
public interface IEmailSender { Task SendAsync(OutgoingEmail message, CancellationToken ct); }
public sealed record SecurityEmail(Guid UserId, string Address, string Subject, string Body);
public interface IRecipientEmailDirectory
{
    Task<string?> FindActiveEmailAsync(Guid user, CancellationToken ct);
    Task<SecurityEmail?> FindSecurityMailAsync(Guid user, Guid ticket, CancellationToken ct) => Task.FromResult<SecurityEmail?>(null);
}
public interface IEmailDeliveryQueue
{
    Task<int> QueueBatchAsync(CancellationToken ct);
    Task<int> SendBatchAsync(CancellationToken ct);
    Task<string> RetryAsync(Guid delivery, Guid actor, byte[] rowVersion, string reason, CancellationToken ct);
}
