using MedEquipSLA.Notification.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace MedEquipSLA.Notification.Infrastructure.Persistence;

public sealed class PersonalNotificationStore(NotificationDbContext db) : IPersonalNotificationQueries, IPersonalNotificationCommands
{
    public async Task<PersonalNotificationPage> ListAsync(Guid recipient, bool unreadOnly, int page, CancellationToken ct)
    {
        const int size = 20;
        var items = await db.Notifications.AsNoTracking()
            .Where(x => x.RecipientUserId == recipient && x.IsInAppVisible && (!unreadOnly || x.ReadAtUtc == null))
            .OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id)
            .Skip((page - 1) * size).Take(size + 1)
            .Select(x => new PersonalNotification(x.Id, x.Kind, x.Title, x.Body, x.ResourceType,
                x.ResourceId, x.CreatedAtUtc, x.ReadAtUtc)).ToListAsync(ct);
        return new(items.Take(size).ToArray(), items.Count > size);
    }

    public async Task<bool> MarkReadAsync(Guid recipient, Guid id, CancellationToken ct)
    {
        var owned = db.Notifications.Where(x => x.RecipientUserId == recipient && x.Id == id && x.IsInAppVisible);
        var now = DateTimeOffset.UtcNow;
        var changed = await owned.Where(x => x.ReadAtUtc == null)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.ReadAtUtc, now), ct);
        return changed != 0 || await owned.AnyAsync(ct);
    }
}
