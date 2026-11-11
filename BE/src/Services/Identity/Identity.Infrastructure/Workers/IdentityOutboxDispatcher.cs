using MedEquipSLA.Contracts.Events;
using MedEquipSLA.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MedEquipSLA.Identity.Infrastructure.Workers;

public sealed class IdentityOutboxDispatcher(IConfiguration config, IServiceScopeFactory scopes, ILogger<IdentityOutboxDispatcher> logger) : BackgroundService
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
                await PublishAsync(scope.ServiceProvider.GetRequiredService<IdentityDbContext>(), scope.ServiceProvider.GetRequiredService<IEventPublisher>(), ct);
            }
            catch (Exception error) when (!ct.IsCancellationRequested) { logger.LogWarning(error, "Identity outbox tick failed."); }
        } while (await timer.WaitForNextTickAsync(ct));
    }
    public static async Task PublishAsync(IdentityDbContext db, IEventPublisher publisher, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var eligible = db.OutboxMessages.Where(x => x.Status == "Pending" && (x.NextAttemptAtUtc == null || x.NextAttemptAtUtc <= now) ||
            x.Status == "Processing" && x.LeaseUntilUtc <= now);
        foreach (var row in await eligible.AsNoTracking().OrderBy(x => x.OccurredAtUtc).Take(20).ToArrayAsync(ct))
        {
            var owner = Guid.NewGuid().ToString("N");
            if (await eligible.Where(x => x.EventId == row.EventId).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "Processing")
                .SetProperty(x => x.LeaseOwner, owner).SetProperty(x => x.LeaseUntilUtc, now.AddMinutes(1))
                .SetProperty(x => x.AttemptCount, x => x.AttemptCount + 1), ct) != 1) continue;
            var owned = db.OutboxMessages.Where(x => x.EventId == row.EventId && x.LeaseOwner == owner);
            try
            {
                var stream = row.EventType == UserAccessChangedEvent.Changed ? UserAccessChangedEvent.Stream :
                    row.EventType == SessionRevokedEvent.Revoked ? SessionRevokedEvent.Stream : IdentityMailEvent.Stream;
                await publisher.PublishAsync(stream, new(row.EventId, row.EventType, row.SchemaVersion,
                    "Identity", row.AggregateId, row.AggregateVersion, row.OccurredAtUtc, row.CorrelationId, row.PayloadJson), ct);
                await owned.ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "Published").SetProperty(x => x.PublishedAtUtc, DateTimeOffset.UtcNow)
                    .SetProperty(x => x.LeaseOwner, (string?)null).SetProperty(x => x.LeaseUntilUtc, (DateTimeOffset?)null), ct);
            }
            catch (Exception error) when (!ct.IsCancellationRequested)
            {
                await owned.ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, row.AttemptCount >= 7 ? "Failed" : "Pending")
                    .SetProperty(x => x.NextAttemptAtUtc, DateTimeOffset.UtcNow.AddSeconds(Math.Min(300, 1 << Math.Min(row.AttemptCount + 1, 8))))
                    .SetProperty(x => x.LeaseOwner, (string?)null).SetProperty(x => x.LeaseUntilUtc, (DateTimeOffset?)null)
                    .SetProperty(x => x.LastError, error.GetType().Name), ct);
            }
        }
    }
}
