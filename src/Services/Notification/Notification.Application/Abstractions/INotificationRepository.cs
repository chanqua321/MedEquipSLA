namespace MedEquipSLA.Notification.Application.Abstractions;
public interface INotificationRepository
{
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken);
}
public interface INotificationQueryAdapter
{
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken);
}
