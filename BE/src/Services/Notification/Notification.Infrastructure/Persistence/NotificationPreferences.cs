using System.Text.Json;
using MedEquipSLA.Contracts.Events;
using MedEquipSLA.Notification.Application.Abstractions;
using MedEquipSLA.Notification.Domain;
using MedEquipSLA.Notification.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace MedEquipSLA.Notification.Infrastructure.Persistence;

public sealed class NotificationPreferences(NotificationDbContext db, TimeProvider time) : INotificationPreferences
{
    public async Task<IReadOnlyList<NotificationPreferenceView>> ListAsync(Guid user, CancellationToken ct) =>
        await db.NotificationPreferences.AsNoTracking().Where(x => x.UserId == user).OrderBy(x => x.NotificationKind)
            .Select(x => new NotificationPreferenceView(x.NotificationKind, x.InAppEnabled, x.EmailEnabled, x.RowVersion)).ToArrayAsync(ct);
    public async Task<bool> SaveAsync(Guid user, string kind, bool inApp, bool email, byte[]? rowVersion, CancellationToken ct)
    {
        if (!UserNotificationEvent.Kinds.Contains(kind) || user == Guid.Empty) return false;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var key = $"NotificationPreference:{user:N}:{kind}";
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @result int;
            EXEC @result=sp_getapplock @Resource={key},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=5000;
            IF @result<0 THROW 51043,'Preference lock unavailable.',1;
            """, ct);
        var row = await db.NotificationPreferences.SingleOrDefaultAsync(x => x.UserId == user && x.NotificationKind == kind, ct);
        if (row is null)
        {
            if (rowVersion is not null) return false;
            db.Add(NotificationPreference.Create(user, kind, inApp, email, time.GetUtcNow()));
        }
        else
        {
            await db.Entry(row).ReloadAsync(ct);
            if (rowVersion is null || !rowVersion.SequenceEqual(row.RowVersion)) return false;
            row.Change(inApp, email, time.GetUtcNow());
        }
        db.Add(AuditEntry.Record("NotificationPreference", user, "PreferenceChanged", time.GetUtcNow(),
            JsonSerializer.Serialize(new { kind, inApp, email }), user));
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return true;
    }
}
