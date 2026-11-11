using MedEquipSLA.Contracts.Events;
using MedEquipSLA.Equipment.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MedEquipSLA.Equipment.Infrastructure.Workers;

public sealed class EquipmentOutboxDispatcher(IConfiguration config, IServiceScopeFactory scopes, ILogger<EquipmentOutboxDispatcher> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!config.GetValue<bool>("Workers:Enabled"))
            return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<EquipmentDbContext>();
                var publisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();
                await PublishBatchAsync(db, publisher, stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(exception, "Equipment outbox cycle failed.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task PublishBatchAsync(EquipmentDbContext db, IEventPublisher publisher, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var eligible = db.OutboxMessages.Where(x => x.SchemaVersion == 1 &&
            (x.EventType == EquipmentDepartmentChanged.EventType || x.EventType == "EquipmentRetired") &&
            (x.Status == "Pending" && (x.NextAttemptAtUtc == null || x.NextAttemptAtUtc <= now) ||
             x.Status == "Processing" && x.LeaseUntilUtc <= now));
        var batch = await eligible.AsNoTracking().OrderBy(x => x.OccurredAtUtc).Take(20).ToListAsync(ct);
        foreach (var item in batch)
        {
            var lease = Guid.NewGuid().ToString("N");
            var claimed = await eligible.Where(x => x.EventId == item.EventId)
                .ExecuteUpdateAsync(update => update.SetProperty(x => x.Status, "Processing")
                    .SetProperty(x => x.LeaseOwner, lease)
                    .SetProperty(x => x.LeaseUntilUtc, DateTimeOffset.UtcNow.AddMinutes(1))
                    .SetProperty(x => x.AttemptCount, x => x.AttemptCount + 1), ct);
            if (claimed == 0)
                continue;

            var owned = db.OutboxMessages.Where(x => x.EventId == item.EventId && x.LeaseOwner == lease);
            try
            {
                await publisher.PublishAsync(item.EventType == "EquipmentRetired" ? "medequip:equipment:lifecycle:v1" : EquipmentDepartmentChanged.Stream, new(item.EventId, item.EventType, item.SchemaVersion,
                    "Equipment", item.AggregateId, item.AggregateVersion, item.OccurredAtUtc,
                    item.CorrelationId, item.PayloadJson), ct);
            }
            catch (Exception exception) when (!ct.IsCancellationRequested)
            {
                var retry = DateTimeOffset.UtcNow.AddSeconds(Math.Min(300, 1 << Math.Min(item.AttemptCount + 1, 9)));
                await owned.ExecuteUpdateAsync(update => update.SetProperty(x => x.Status, "Pending")
                    .SetProperty(x => x.NextAttemptAtUtc, retry)
                    .SetProperty(x => x.LeaseOwner, (string?)null)
                    .SetProperty(x => x.LeaseUntilUtc, (DateTimeOffset?)null)
                    .SetProperty(x => x.LastError, exception.GetType().Name), ct);
                logger.LogWarning("Publishing Equipment event {EventId} failed. Retry scheduled at {RetryAt}", item.EventId, retry);
                continue;
            }
            await owned.ExecuteUpdateAsync(update => update.SetProperty(x => x.Status, "Published")
                .SetProperty(x => x.PublishedAtUtc, DateTimeOffset.UtcNow)
                .SetProperty(x => x.NextAttemptAtUtc, (DateTimeOffset?)null)
                .SetProperty(x => x.LeaseOwner, (string?)null)
                .SetProperty(x => x.LeaseUntilUtc, (DateTimeOffset?)null)
                .SetProperty(x => x.LastError, (string?)null), ct);
            logger.LogInformation("Published Equipment event {EventId}", item.EventId);
        }
    }
}
