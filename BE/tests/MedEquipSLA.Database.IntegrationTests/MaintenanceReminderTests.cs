using MedEquipSLA.Contracts.Events;
using MedEquipSLA.Maintenance.Domain;
using MedEquipSLA.Maintenance.Infrastructure.Persistence;
using MedEquipSLA.Maintenance.Infrastructure.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MedEquipSLA.Database.IntegrationTests;

public sealed class MaintenanceReminderTests
{
    internal static readonly DateTimeOffset Scheduled = new(2027, 1, 10, 10, 0, 0, TimeSpan.Zero);
    private static MaintenanceDbContext Context(TestDatabase db) =>
        (MaintenanceDbContext)Contexts.Create("Maintenance", db.ConnectionString);

    internal static async Task<ScheduleOccurrence> SeedAsync(MaintenanceDbContext db)
    {
        var schedule = MaintenanceSchedule.Create(Guid.NewGuid(), Guid.NewGuid(), "EQ-1", "Monitor",
            Guid.NewGuid(), "ICU", Guid.NewGuid(), "Calibration", Scheduled, 7, 60, 30, true, Guid.NewGuid(), Scheduled.AddDays(-1));
        var occurrence = schedule.PlanNext(Scheduled.AddMinutes(-60));
        db.AddRange(schedule, occurrence);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return occurrence;
    }

    private static ReminderPlanner Planner(MaintenanceDbContext db, INotificationRecipientLookup? lookup = null) =>
        new(db, lookup ?? new RecipientStub(), NullLogger<ReminderPlanner>.Instance);

    [SqlServerTheory]
    [InlineData(-61, null)]
    [InlineData(-60, "DueSoon")]
    [InlineData(-1, "DueSoon")]
    [InlineData(0, null)]
    [InlineData(15, null)]
    [InlineData(30, "Overdue")]
    [InlineData(60, "Overdue")]
    public async Task Boundaries_and_idempotency_use_occurrence_snapshot(int offset, string? expected)
    {
        await using var database = await TestDatabase.CreateAsync("Maintenance");
        await using var db = Context(database);
        await db.Database.MigrateAsync();
        var occurrence = await SeedAsync(db);
        var now = Scheduled.AddMinutes(offset);
        Assert.Equal(expected is null ? 0 : 1, await Planner(db).ScanAsync(now, default));
        Assert.Equal(0, await Planner(db).ScanAsync(now, default));
        var reminders = await db.OccurrenceReminders.AsNoTracking().ToListAsync();
        Assert.Equal(expected is null ? 0 : 1, reminders.Count);
        if (expected is not null)
        {
            var reminder = Assert.Single(reminders);
            Assert.Equal(expected, reminder.ReminderKind);
            Assert.Equal(now, reminder.TriggeredAtUtc);
            var message = await db.OutboxMessages.AsNoTracking().SingleAsync();
            Assert.Equal(reminder.EventId, message.EventId);
            Assert.Equal(occurrence.Id, message.AggregateId);
            Assert.Equal("Pending", message.Status);
        }
        Assert.Empty(await db.WorkOrders.ToListAsync());
    }

    [SqlServerFact]
    public async Task Inactive_reactivation_and_schedule_edits_preserve_old_window_and_deadline()
    {
        await using var database = await TestDatabase.CreateAsync("Maintenance");
        await using var db = Context(database);
        await db.Database.MigrateAsync();
        var original = await SeedAsync(db);
        var schedule = await db.MaintenanceSchedules.SingleAsync();
        schedule.Revise(Scheduled, schedule.NextOccurrenceAtUtc, 7, 0, 500, false, Guid.NewGuid(), Scheduled.AddMinutes(-59));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        Assert.Equal(0, await Planner(db).ScanAsync(Scheduled.AddMinutes(-30), default));
        schedule = await db.MaintenanceSchedules.SingleAsync();
        schedule.Revise(Scheduled, schedule.NextOccurrenceAtUtc, 7, 0, 500, true, Guid.NewGuid(), Scheduled.AddMinutes(1));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        Assert.Equal(0, await Planner(db).ScanAsync(Scheduled.AddMinutes(1), default));
        Assert.Equal(1, await Planner(db).ScanAsync(Scheduled.AddMinutes(30), default));
        var actual = await db.ScheduleOccurrences.AsNoTracking().SingleAsync();
        Assert.Equal(original.DueAtUtc, actual.DueAtUtc);
        Assert.Equal(original.ReminderWindowStartAtUtc, actual.ReminderWindowStartAtUtc);
        Assert.Equal("Overdue", (await db.OccurrenceReminders.SingleAsync()).ReminderKind);
    }

    [SqlServerFact]
    public async Task Active_schedule_edit_does_not_move_an_existing_reminder_window()
    {
        await using var database = await TestDatabase.CreateAsync("Maintenance");
        await using var db = Context(database);
        await db.Database.MigrateAsync();
        await SeedAsync(db);
        var schedule = await db.MaintenanceSchedules.SingleAsync();
        schedule.Revise(Scheduled, schedule.NextOccurrenceAtUtc, 7, 1, 999, true, Guid.NewGuid(), Scheduled.AddMinutes(-59));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        Assert.Equal(1, await Planner(db).ScanAsync(Scheduled.AddMinutes(-30), default));
        Assert.Equal("DueSoon", (await db.OccurrenceReminders.SingleAsync()).ReminderKind);
        Assert.Equal(Scheduled.AddMinutes(30), (await db.ScheduleOccurrences.SingleAsync()).DueAtUtc);
    }

    [SqlServerFact]
    public async Task Legacy_window_skips_due_soon_but_keeps_overdue()
    {
        await using var database = await TestDatabase.CreateAsync("Maintenance");
        await using var db = Context(database);
        await db.Database.MigrateAsync();
        await SeedAsync(db);
        await db.ScheduleOccurrences.ExecuteUpdateAsync(update => update.SetProperty(x => x.ReminderWindowStartAtUtc, (DateTimeOffset?)null));
        Assert.Equal(0, await Planner(db).ScanAsync(Scheduled.AddMinutes(-30), default));
        Assert.Equal(1, await Planner(db).ScanAsync(Scheduled.AddMinutes(30), default));
    }

    [SqlServerFact]
    public async Task Concurrent_scanners_record_one_reminder_and_one_outbox()
    {
        await using var database = await TestDatabase.CreateAsync("Maintenance");
        await using var first = Context(database);
        await using var second = Context(database);
        await first.Database.MigrateAsync();
        await SeedAsync(first);
        var counts = await Task.WhenAll(Planner(first).ScanAsync(Scheduled.AddMinutes(-30), default),
            Planner(second).ScanAsync(Scheduled.AddMinutes(-30), default));
        Assert.Equal(1, counts.Sum());
        Assert.Single(await first.OccurrenceReminders.ToListAsync());
        Assert.Single(await first.OutboxMessages.ToListAsync());
    }

    [SqlServerFact]
    public async Task Sql_failure_rolls_back_reminder_and_retry_after_restart_records_one()
    {
        await using var database = await TestDatabase.CreateAsync("Maintenance");
        await using var db = Context(database);
        await db.Database.MigrateAsync();
        await SeedAsync(db);
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.OutboxMessages ADD CONSTRAINT TestReminderFailure CHECK (EventType <> 'MaintenanceDueSoon')");
        Assert.Equal(0, await Planner(db).ScanAsync(Scheduled.AddMinutes(-30), default));
        Assert.Empty(await db.OccurrenceReminders.ToListAsync());
        Assert.Empty(await db.OutboxMessages.ToListAsync());
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.OutboxMessages DROP CONSTRAINT TestReminderFailure");
        await using var restarted = Context(database);
        Assert.Equal(1, await Planner(restarted).ScanAsync(Scheduled.AddMinutes(-30), default));
    }

    [SqlServerFact]
    public async Task Assigned_technician_is_read_from_work_order_and_missing_assignment_uses_admin_lookup()
    {
        await using var database = await TestDatabase.CreateAsync("Maintenance");
        await using var db = Context(database);
        await db.Database.MigrateAsync();
        var occurrence = await SeedAsync(db);
        var recipients = new RecipientStub();
        Assert.Equal(1, await Planner(db, recipients).ScanAsync(Scheduled.AddMinutes(-30), default));
        Assert.Null(Assert.Single(recipients.Technicians));
        var schedule = await db.MaintenanceSchedules.AsNoTracking().SingleAsync();
        var technician = Guid.NewGuid();
        var work = new WorkOrder();
        db.Add(work);
        db.Entry(work).CurrentValues.SetValues(new
        {
            Code = "WO-1", OccurrenceId = (Guid?)occurrence.Id, schedule.EquipmentId,
            EquipmentCodeSnapshot = "EQ", EquipmentNameSnapshot = "Monitor",
            DepartmentIdSnapshot = schedule.DepartmentIdSnapshot, DepartmentNameSnapshot = "ICU",
            WorkType = "Calibration", Status = "Scheduled", AssignedTechnicianId = (Guid?)technician,
            ScheduledForUtc = Scheduled, DueAtUtc = Scheduled.AddMinutes(30),
            EquipmentUseRegistrationId = Guid.NewGuid(), CreatedAtUtc = Scheduled, UpdatedAtUtc = Scheduled
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        Assert.Equal(1, await Planner(db, recipients).ScanAsync(Scheduled.AddMinutes(30), default));
        Assert.Equal(technician, recipients.Technicians.Last());
    }

    [SqlServerFact]
    public async Task Publish_failure_preserves_event_id_and_two_dispatchers_share_sql_lease()
    {
        await using var database = await TestDatabase.CreateAsync("Maintenance");
        await using var db = Context(database);
        await db.Database.MigrateAsync();
        await SeedAsync(db);
        await Planner(db).ScanAsync(Scheduled.AddMinutes(-30), default);
        var eventId = (await db.OutboxMessages.AsNoTracking().SingleAsync()).EventId;
        await using var services = new ServiceCollection().BuildServiceProvider();
        using var dispatcher = new ScheduleOutboxDispatcher(new ConfigurationBuilder().Build(),
            services.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System, NullLogger<ScheduleOutboxDispatcher>.Instance);
        var publisher = new RecordingPublisher();
        await dispatcher.PublishBatchAsync(db, publisher, default);
        var pending = await db.OutboxMessages.AsNoTracking().SingleAsync();
        Assert.Equal("Pending", pending.Status);
        Assert.NotNull(pending.NextAttemptAtUtc);
        await db.OutboxMessages.ExecuteUpdateAsync(update => update.SetProperty(x => x.NextAttemptAtUtc, DateTimeOffset.UtcNow.AddMinutes(-1)));
        await using var other = Context(database);
        await Task.WhenAll(dispatcher.PublishBatchAsync(db, publisher, default), dispatcher.PublishBatchAsync(other, publisher, default));
        Assert.Equal(new[] { eventId, eventId }, publisher.EventIds);
        var published = await db.OutboxMessages.AsNoTracking().SingleAsync();
        Assert.Equal("Published", published.Status);
        Assert.Equal(2, published.AttemptCount);
    }

    [SqlServerFact]
    public async Task Delayed_due_soon_is_deferred_while_inactive_then_retained_as_expired()
    {
        await using var database = await TestDatabase.CreateAsync("Maintenance");
        await using var db = Context(database);
        await db.Database.MigrateAsync();
        await SeedAsync(db);
        await Planner(db).ScanAsync(Scheduled.AddMinutes(-30), default);
        var schedule = await db.MaintenanceSchedules.SingleAsync();
        schedule.Revise(Scheduled, schedule.NextOccurrenceAtUtc, 7, 60, 30, false, Guid.NewGuid(), Scheduled.AddMinutes(-20));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        await using var services = new ServiceCollection().BuildServiceProvider();
        var time = new FixedTime(Scheduled.AddMinutes(-10));
        using var dispatcher = new ScheduleOutboxDispatcher(new ConfigurationBuilder().Build(),
            services.GetRequiredService<IServiceScopeFactory>(), time, NullLogger<ScheduleOutboxDispatcher>.Instance);
        var publisher = new RecordingPublisher();
        await dispatcher.PublishBatchAsync(db, publisher, default);
        Assert.Empty(publisher.EventIds);
        Assert.Equal("Pending", (await db.OutboxMessages.AsNoTracking().SingleAsync()).Status);
        await db.OutboxMessages.ExecuteUpdateAsync(update => update.SetProperty(x => x.NextAttemptAtUtc, Scheduled.AddMinutes(-1)));
        time.Now = Scheduled;
        await dispatcher.PublishBatchAsync(db, publisher, default);
        var expired = await db.OutboxMessages.AsNoTracking().SingleAsync();
        Assert.Equal("Failed", expired.Status);
        Assert.Equal("DueSoonExpired", expired.LastError);
        Assert.Empty(publisher.EventIds);
        Assert.Single(await db.OccurrenceReminders.ToListAsync());
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    internal sealed class RecipientStub : INotificationRecipientLookup
    {
        private readonly Guid admin = Guid.NewGuid();
        public List<Guid?> Technicians { get; } = [];
        public Task<Guid[]> ResolveAsync(Guid? technicianId, CancellationToken ct)
        {
            Technicians.Add(technicianId);
            return Task.FromResult(technicianId is { } id ? new[] { admin, id } : new[] { admin });
        }
    }

    private sealed class RecordingPublisher : IEventPublisher
    {
        public List<Guid> EventIds { get; } = [];
        public Task PublishAsync(string stream, IntegrationEvent message, CancellationToken ct)
        {
            Assert.Equal(MaintenanceReminderEvent.Stream, stream);
            lock (EventIds)
            {
                EventIds.Add(message.EventId);
                return EventIds.Count == 1 ? Task.FromException(new IOException("Test Redis outage")) : Task.CompletedTask;
            }
        }
    }
}
