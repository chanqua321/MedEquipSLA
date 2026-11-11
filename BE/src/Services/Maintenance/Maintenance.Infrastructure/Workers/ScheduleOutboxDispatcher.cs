using System.Text.Json;
using MedEquipSLA.Contracts.Events;
using MedEquipSLA.Maintenance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MedEquipSLA.Maintenance.Infrastructure.Workers;

public sealed class ScheduleOutboxDispatcher(IConfiguration config, IServiceScopeFactory scopes,
    TimeProvider time, ILogger<ScheduleOutboxDispatcher> logger)
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
                var db = scope.ServiceProvider.GetRequiredService<MaintenanceDbContext>();
                var publisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();
                await PublishBatchAsync(db, publisher, stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(exception, "Maintenance outbox cycle failed.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task PublishBatchAsync(MaintenanceDbContext db, IEventPublisher publisher, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var eligible = db.OutboxMessages.Where(x => x.SchemaVersion == 1 &&
            (x.EventType == MaintenanceScheduleEvent.Created || x.EventType == MaintenanceScheduleEvent.Updated ||
             x.EventType == MaintenanceScheduleEvent.Deactivated || x.EventType == MaintenanceScheduleEvent.Ended || x.EventType == MaintenanceReminderEvent.DueSoon ||
             x.EventType == MaintenanceReminderEvent.Overdue || x.EventType == WorkOrderLifecycleEvent.Created ||
             x.EventType == WorkOrderLifecycleEvent.Updated || x.EventType == UserNotificationEvent.Requested || x.EventType == RegistrationSnapshotPage.EventType) &&
            (x.Status == "Pending" && (x.NextAttemptAtUtc == null || x.NextAttemptAtUtc <= now) ||
             x.Status == "Processing" && x.LeaseUntilUtc <= now));
        var batch = await eligible.AsNoTracking().OrderBy(x => x.OccurredAtUtc).Take(20).ToListAsync(ct);
        foreach (var item in batch)
        {
            var lease = Guid.NewGuid().ToString("N");
            var claimed = await eligible.Where(x => x.EventId == item.EventId)
                .ExecuteUpdateAsync(update => update.SetProperty(x => x.Status, "Processing")
                    .SetProperty(x => x.LeaseOwner, lease)
                    .SetProperty(x => x.LeaseUntilUtc, time.GetUtcNow().AddMinutes(1))
                    .SetProperty(x => x.AttemptCount, x => x.AttemptCount + 1), ct);
            if (claimed == 0)
                continue;

            var owned = db.OutboxMessages.Where(x => x.EventId == item.EventId && x.LeaseOwner == lease);
            try
            {
                if (item.EventType == MaintenanceReminderEvent.DueSoon)
                {
                    var reminder = JsonSerializer.Deserialize<MaintenanceReminderEvent>(item.PayloadJson)
                        ?? throw new JsonException("Invalid reminder outbox payload.");
                    if (time.GetUtcNow() >= reminder.ScheduledOccurrenceAtUtc)
                    {
                        await owned.ExecuteUpdateAsync(update => update.SetProperty(x => x.Status, "Failed")
                            .SetProperty(x => x.LastError, "DueSoonExpired")
                            .SetProperty(x => x.LeaseOwner, (string?)null)
                            .SetProperty(x => x.LeaseUntilUtc, (DateTimeOffset?)null), ct);
                        logger.LogInformation("Suppressed expired DueSoon event {EventId}; reminder history is retained.", item.EventId);
                        continue;
                    }
                    if (!await db.MaintenanceSchedules.AnyAsync(x => x.Id == reminder.ScheduleId && x.IsActive, ct))
                    {
                        await owned.ExecuteUpdateAsync(update => update.SetProperty(x => x.Status, "Pending")
                            .SetProperty(x => x.NextAttemptAtUtc, time.GetUtcNow().AddMinutes(1))
                            .SetProperty(x => x.LastError, "ScheduleInactive")
                            .SetProperty(x => x.LeaseOwner, (string?)null)
                            .SetProperty(x => x.LeaseUntilUtc, (DateTimeOffset?)null), ct);
                        logger.LogInformation("Deferred DueSoon event {EventId} while its schedule is inactive.", item.EventId);
                        continue;
                    }
                }
                var stream = item.EventType == RegistrationSnapshotPage.EventType ? RegistrationSnapshotPage.Stream : item.EventType is MaintenanceReminderEvent.DueSoon or MaintenanceReminderEvent.Overdue
                    ? MaintenanceReminderEvent.Stream : item.EventType is WorkOrderLifecycleEvent.Created or WorkOrderLifecycleEvent.Updated
                        ? WorkOrderLifecycleEvent.Stream : item.EventType == UserNotificationEvent.Requested
                            ? UserNotificationEvent.Stream : MaintenanceScheduleEvent.Stream;
                await publisher.PublishAsync(stream, new(item.EventId, item.EventType, item.SchemaVersion,
                    "Maintenance", item.AggregateId, item.AggregateVersion, item.OccurredAtUtc,
                    item.CorrelationId, item.PayloadJson), ct);
            }
            catch (Exception exception) when (!ct.IsCancellationRequested)
            {
                var retry = time.GetUtcNow().AddSeconds(Math.Min(300, 1 << Math.Min(item.AttemptCount + 1, 9)));
                await owned.ExecuteUpdateAsync(update => update.SetProperty(x => x.Status, "Pending")
                    .SetProperty(x => x.NextAttemptAtUtc, retry)
                    .SetProperty(x => x.LeaseOwner, (string?)null)
                    .SetProperty(x => x.LeaseUntilUtc, (DateTimeOffset?)null)
                    .SetProperty(x => x.LastError, exception.GetType().Name), ct);
                logger.LogWarning("Publishing Maintenance event {EventId} failed. Retry scheduled at {RetryAt}", item.EventId, retry);
                continue;
            }
            await owned.ExecuteUpdateAsync(update => update.SetProperty(x => x.Status, "Published")
                .SetProperty(x => x.PublishedAtUtc, time.GetUtcNow())
                .SetProperty(x => x.NextAttemptAtUtc, (DateTimeOffset?)null)
                .SetProperty(x => x.LeaseOwner, (string?)null)
                .SetProperty(x => x.LeaseUntilUtc, (DateTimeOffset?)null)
                .SetProperty(x => x.LastError, (string?)null), ct);
            logger.LogInformation("Published Maintenance event {EventId}", item.EventId);
        }
    }
}
