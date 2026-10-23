using Microsoft.EntityFrameworkCore;
using MedEquipSLA.Notification.Application.Abstractions;
namespace MedEquipSLA.Notification.Infrastructure.Persistence;
public sealed class NotificationRepository(NotificationDbContext db) : INotificationRepository
{ public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) => db.Records.AnyAsync(x => x.Id == id, cancellationToken); }
public sealed class NotificationQueryAdapter(NotificationDbContext db) : INotificationQueryAdapter
{ public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) => db.Records.AsNoTracking().AnyAsync(x => x.Id == id, cancellationToken); }
