namespace MedEquipSLA.Notification.Application.Abstractions;

public sealed record PersonalNotification(Guid Id, string Kind, string Title, string Body, string ResourceType,
    Guid ResourceId, DateTimeOffset CreatedAtUtc, DateTimeOffset? ReadAtUtc);
public sealed record PersonalNotificationPage(IReadOnlyList<PersonalNotification> Items, bool HasNext);

public interface IPersonalNotificationQueries
{
    Task<PersonalNotificationPage> ListAsync(Guid recipient, bool unreadOnly, int page, CancellationToken ct);
}

public interface IPersonalNotificationCommands
{
    Task<bool> MarkReadAsync(Guid recipient, Guid id, CancellationToken ct);
}
