using MedEquipSLA.Notification.Infrastructure.Persistence;
using MedEquipSLA.Notification.Infrastructure.Realtime;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MedEquipSLA.Notification.Infrastructure.Workers;

public sealed class NotificationHintWorker(IServiceScopeFactory scopes, IHubContext<NotificationHub> hub,
    IConfiguration config, ILogger<NotificationHintWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!config.GetValue<bool>("Workers:Enabled")) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
                var rows = await db.Notifications.AsNoTracking().Where(x => x.IsInAppVisible && x.RealtimeNotifiedAtUtc == null)
                    .OrderBy(x => x.Id).Select(x => new { x.Id, x.RecipientUserId }).Take(100).ToArrayAsync(ct);
                foreach (var row in rows)
                {
                    // A hint follows SQL commit; REST is the durable source after reconnect.
                    await hub.Clients.User(row.RecipientUserId.ToString()).SendAsync("notificationsChanged", new { notificationId = row.Id }, ct);
                    await db.Notifications.Where(x => x.Id == row.Id && x.RealtimeNotifiedAtUtc == null)
                        .ExecuteUpdateAsync(update => update.SetProperty(x => x.RealtimeNotifiedAtUtc, DateTimeOffset.UtcNow), ct);
                }
            }
            catch (Exception failure) when (!ct.IsCancellationRequested)
            { logger.LogWarning(failure, "Realtime hint failed; durable notifications remain available through REST."); }
        } while (await timer.WaitForNextTickAsync(ct));
    }
}
