using System.Text.Json;
using MedEquipSLA.Contracts.Events;
using MedEquipSLA.Notification.Infrastructure.Persistence;
using MedEquipSLA.Notification.Infrastructure.Workers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MedEquipSLA.Database.IntegrationTests;

public sealed class MaintenanceReminderInboxTests
{
    internal static (IntegrationEvent Message, MaintenanceReminderEvent Payload) Event(Guid[] recipients)
    {
        var scheduled = MaintenanceReminderTests.Scheduled;
        var payload = new MaintenanceReminderEvent(Guid.NewGuid(), Guid.NewGuid(), "EQ-1", "Monitor", Guid.NewGuid(),
            scheduled, scheduled.AddMinutes(30), scheduled.AddHours(-1), "DueSoon", recipients.LastOrDefault(), recipients);
        return (new IntegrationEvent(Guid.NewGuid(), MaintenanceReminderEvent.DueSoon, 1, "Maintenance", Guid.NewGuid(),
            1, scheduled.AddMinutes(-30), Guid.NewGuid().ToString(), JsonSerializer.Serialize(payload)), payload);
    }

    [SqlServerFact]
    public async Task One_event_records_admin_and_technician_once_and_excludes_inactive_recipients()
    {
        await using var database = await TestDatabase.CreateAsync("Notification");
        await using var db = (NotificationDbContext)Contexts.Create("Notification", database.ConnectionString);
        await db.Database.MigrateAsync();
        var admin = Guid.NewGuid();
        var technician = Guid.NewGuid();
        var inactive = Guid.NewGuid();
        var (message, payload) = Event([admin, technician, inactive]);
        var now = MaintenanceReminderTests.Scheduled.AddMinutes(-30);
        await NotificationConsumer.StoreReminderAsync(db, message, payload, [admin, technician], now, default);
        await NotificationConsumer.StoreReminderAsync(db, message, payload, [admin, technician], now, default);
        Assert.Equal(2, await db.Notifications.CountAsync());
        Assert.Equal(1, await db.InboxMessages.CountAsync());
        Assert.False(await db.Notifications.AnyAsync(x => x.RecipientUserId == inactive));
        Assert.All(await db.Notifications.ToListAsync(), row =>
        {
            Assert.Equal(message.EventId, row.SourceEventId);
            Assert.Equal("ScheduleOccurrence", row.ResourceType);
            Assert.Equal(message.AggregateId, row.ResourceId);
        });
    }

    [SqlServerFact]
    public async Task Inbox_failure_rolls_back_notifications_and_retry_after_restart_stores_them()
    {
        await using var database = await TestDatabase.CreateAsync("Notification");
        await using var db = (NotificationDbContext)Contexts.Create("Notification", database.ConnectionString);
        await db.Database.MigrateAsync();
        var admin = Guid.NewGuid();
        var (message, payload) = Event([admin]);
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.InboxMessages ADD CONSTRAINT TestReminderInboxFailure CHECK (EventType <> 'MaintenanceDueSoon')");
        await Assert.ThrowsAsync<DbUpdateException>(() => NotificationConsumer.StoreReminderAsync(db, message, payload,
            [admin], MaintenanceReminderTests.Scheduled.AddMinutes(-30), default));
        Assert.Empty(await db.Notifications.AsNoTracking().ToListAsync());
        Assert.Empty(await db.InboxMessages.AsNoTracking().ToListAsync());
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.InboxMessages DROP CONSTRAINT TestReminderInboxFailure");
        await using var restarted = (NotificationDbContext)Contexts.Create("Notification", database.ConnectionString);
        await NotificationConsumer.StoreReminderAsync(restarted, message, payload, [admin],
            MaintenanceReminderTests.Scheduled.AddMinutes(-30), default);
        Assert.Single(await restarted.Notifications.ToListAsync());
        Assert.Single(await restarted.InboxMessages.ToListAsync());
    }

    [SqlServerFact]
    public async Task Due_soon_delayed_in_redis_is_recorded_in_inbox_without_stale_notification()
    {
        await using var database = await TestDatabase.CreateAsync("Notification");
        await using var db = (NotificationDbContext)Contexts.Create("Notification", database.ConnectionString);
        await db.Database.MigrateAsync();
        var admin = Guid.NewGuid();
        var (message, payload) = Event([admin]);
        await NotificationConsumer.StoreReminderAsync(db, message, payload, [admin], payload.ScheduledOccurrenceAtUtc, default);
        Assert.Empty(await db.Notifications.ToListAsync());
        Assert.Single(await db.InboxMessages.ToListAsync());
    }

    [SqlServerFact]
    public async Task Incident_sla_storage_keeps_its_original_consumer_group_and_deduplicates()
    {
        await using var database = await TestDatabase.CreateAsync("Notification");
        await using var db = (NotificationDbContext)Contexts.Create("Notification", database.ConnectionString);
        await db.Database.MigrateAsync();
        var payload = new SlaAlertEvent(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-1));
        var message = new IntegrationEvent(Guid.NewGuid(), SlaAlertEvent.ResponseBreached, 1, "Incident", Guid.NewGuid(),
            1, DateTimeOffset.UtcNow, "test", JsonSerializer.Serialize(payload));
        await NotificationConsumer.StoreAsync(db, message, payload, default);
        await NotificationConsumer.StoreAsync(db, message, payload, default);
        Assert.Equal("notification-sla-v1", (await db.InboxMessages.SingleAsync()).ConsumerName);
        Assert.Single(await db.Notifications.ToListAsync());
    }
}
