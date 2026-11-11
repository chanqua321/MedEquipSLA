namespace MedEquipSLA.Notification.Application.Abstractions;

public sealed record NotificationPreferenceView(string Kind, bool InAppEnabled, bool EmailEnabled, byte[] RowVersion);
public interface INotificationPreferences
{
    Task<IReadOnlyList<NotificationPreferenceView>> ListAsync(Guid user, CancellationToken ct);
    Task<bool> SaveAsync(Guid user, string kind, bool inApp, bool email, byte[]? rowVersion, CancellationToken ct);
}
