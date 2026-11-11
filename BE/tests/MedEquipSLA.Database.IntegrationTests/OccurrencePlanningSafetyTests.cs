using MedEquipSLA.Maintenance.Domain;
using MedEquipSLA.Maintenance.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace MedEquipSLA.Database.IntegrationTests;

public sealed class OccurrencePlanningSafetyTests
{
    private static readonly DateTimeOffset Anchor = new(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);

    private static MaintenanceDbContext Context(TestDatabase database) =>
        (MaintenanceDbContext)Contexts.Create("Maintenance", database.ConnectionString);

    private static MaintenanceSchedule Schedule(int number, int interval = 1) =>
        MaintenanceSchedule.Create(Guid.Parse($"00000000-0000-0000-0000-{number:D12}"), Guid.NewGuid(),
            "EQ", "Monitor", Guid.NewGuid(), "ICU", Guid.NewGuid(), "Calibration",
            Anchor, interval, 0, 30, true, Guid.NewGuid(), Anchor.AddDays(-10));

    private static OccurrencePlanner Planner(MaintenanceDbContext db, OccurrencePlanningCursor? cursor = null,
        int batch = 50, int perSchedule = 4) =>
        new(db, cursor ?? new(), Options.Create(new OccurrencePlanningOptions
        {
            ScheduleBatchSize = batch, OccurrencesPerSchedule = perSchedule
        }), NullLogger<OccurrencePlanner>.Instance);

    [SqlServerFact]
    public async Task Exact_boundary_utc_catchup_and_restart_preserve_original_times()
    {
        await using var database = await TestDatabase.CreateAsync("Maintenance");
        await using var db = Context(database);
        await db.Database.MigrateAsync();
        db.Add(Schedule(1));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        Assert.Equal(0, await Planner(db).PlanBatchAsync(Anchor.AddTicks(-1), default));
        Assert.Equal(1, await Planner(db).PlanBatchAsync(Anchor.ToOffset(TimeSpan.FromHours(7)), default));
        Assert.Equal(0, await Planner(db).PlanBatchAsync(Anchor, default));
        Assert.Equal(4, await Planner(db, perSchedule: 4).PlanBatchAsync(Anchor.AddDays(10), default));

        // A new context and selection cursor represent a restarted worker.
        await using var restarted = Context(database);
        Assert.Equal(4, await Planner(restarted).PlanBatchAsync(Anchor.AddDays(10), default));
        var occurrences = await restarted.ScheduleOccurrences.OrderBy(x => x.ScheduledOccurrenceAtUtc).ToListAsync();
        Assert.Equal(9, occurrences.Count);
        for (var index = 0; index < occurrences.Count; index++)
        {
            Assert.Equal(Anchor.AddDays(index), occurrences[index].ScheduledOccurrenceAtUtc);
            Assert.Equal(Anchor.AddDays(index).AddMinutes(30), occurrences[index].DueAtUtc);
            Assert.Equal(TimeSpan.Zero, occurrences[index].CreatedAtUtc.Offset);
            Assert.Equal("Planned", occurrences[index].State);
        }
        Assert.Empty(await restarted.WorkOrders.ToListAsync());
    }

    [SqlServerFact]
    public async Task Keyset_rotation_gives_other_schedules_a_turn_and_enforces_both_limits()
    {
        await using var database = await TestDatabase.CreateAsync("Maintenance");
        await using var db = Context(database);
        await db.Database.MigrateAsync();
        var rows = Enumerable.Range(1, 5).Select(number => Schedule(number)).ToArray();
        db.AddRange(rows);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var cursor = new OccurrencePlanningCursor();

        Assert.Equal(4, await Planner(db, cursor, batch: 2, perSchedule: 2).PlanBatchAsync(Anchor.AddDays(100), default));
        Assert.Equal(4, await Planner(db, cursor, batch: 2, perSchedule: 2).PlanBatchAsync(Anchor.AddDays(100), default));
        Assert.Equal(2, await Planner(db, cursor, batch: 2, perSchedule: 2).PlanBatchAsync(Anchor.AddDays(100), default));
        var groups = await db.ScheduleOccurrences.GroupBy(x => x.ScheduleId)
            .Select(group => new { Id = group.Key, Count = group.Count() }).ToListAsync();
        Assert.Equal(5, groups.Count);
        Assert.All(groups, group => Assert.Equal(2, group.Count));
        Assert.Equal(4, await Planner(db, cursor, batch: 2, perSchedule: 2).PlanBatchAsync(Anchor.AddDays(100), default));
        Assert.Equal(14, await db.ScheduleOccurrences.CountAsync());
    }

    [SqlServerFact]
    public async Task Inactive_keeps_planned_history_and_reactivation_skips_unplanned_inactive_period()
    {
        await using var database = await TestDatabase.CreateAsync("Maintenance");
        await using var db = Context(database);
        await db.Database.MigrateAsync();
        var row = Schedule(1);
        db.Add(row);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        Assert.Equal(1, await Planner(db).PlanBatchAsync(Anchor, default));
        var original = await db.ScheduleOccurrences.AsNoTracking().SingleAsync();

        row = await db.MaintenanceSchedules.SingleAsync();
        row.Revise(Anchor, row.NextOccurrenceAtUtc, 1, 0, 30, false, Guid.NewGuid(), Anchor.AddHours(1));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        Assert.Equal(0, await Planner(db).PlanBatchAsync(Anchor.AddDays(3), default));

        row = await db.MaintenanceSchedules.SingleAsync();
        row.Revise(Anchor, row.NextOccurrenceAtUtc, 1, 0, 30, true, Guid.NewGuid(), Anchor.AddDays(3).AddHours(1));
        Assert.Equal(Anchor.AddDays(4), row.NextOccurrenceAtUtc);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        Assert.Equal(0, await Planner(db).PlanBatchAsync(Anchor.AddDays(3).AddHours(1), default));
        Assert.Equal(1, await Planner(db).PlanBatchAsync(Anchor.AddDays(4), default));
        var old = await db.ScheduleOccurrences.AsNoTracking().SingleAsync(x => x.Id == original.Id);
        Assert.Equal(original.DueAtUtc, old.DueAtUtc);
        Assert.Equal(original.ScheduledOccurrenceAtUtc, old.ScheduledOccurrenceAtUtc);
        Assert.Equal("Planned", old.State);
        Assert.Equal(new[] { Anchor, Anchor.AddDays(4) }, await db.ScheduleOccurrences
            .OrderBy(x => x.ScheduledOccurrenceAtUtc).Select(x => x.ScheduledOccurrenceAtUtc).ToArrayAsync());
    }

    [Fact]
    public void Reactivation_at_exact_boundary_includes_that_boundary_and_respects_future_cursor()
    {
        var row = Schedule(1, interval: 7);
        row.Revise(Anchor, Anchor, 7, 0, 30, false, Guid.NewGuid(), Anchor);
        row.Revise(Anchor, Anchor, 7, 0, 30, true, Guid.NewGuid(), Anchor.AddDays(14));
        Assert.Equal(Anchor.AddDays(14), row.NextOccurrenceAtUtc);
        row.Revise(Anchor, Anchor.AddDays(28), 7, 0, 30, false, Guid.NewGuid(), Anchor.AddDays(14));
        row.Revise(Anchor, Anchor.AddDays(28), 7, 0, 30, true, Guid.NewGuid(), Anchor.AddDays(15));
        Assert.Equal(Anchor.AddDays(28), row.NextOccurrenceAtUtc);
    }

    [SqlServerFact]
    public async Task Failure_rolls_back_cursor_continues_other_schedules_and_retry_after_restart_succeeds()
    {
        await using var database = await TestDatabase.CreateAsync("Maintenance");
        await using var db = Context(database);
        await db.Database.MigrateAsync();
        var broken = Schedule(1);
        var healthy = Schedule(2);
        db.AddRange(broken, healthy);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        await db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE dbo.ScheduleOccurrences ADD CONSTRAINT TestPlanningFailure CHECK (ScheduleId <> '00000000-0000-0000-0000-000000000001')");
        Assert.Equal(1, await Planner(db).PlanBatchAsync(Anchor, default));
        var failed = await db.MaintenanceSchedules.AsNoTracking().SingleAsync(x => x.Id == broken.Id);
        Assert.Equal(Anchor, failed.NextOccurrenceAtUtc);
        Assert.False(await db.ScheduleOccurrences.AnyAsync(x => x.ScheduleId == broken.Id));
        Assert.True(await db.ScheduleOccurrences.AnyAsync(x => x.ScheduleId == healthy.Id));
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.ScheduleOccurrences DROP CONSTRAINT TestPlanningFailure");
        await using var restarted = Context(database);
        Assert.Equal(1, await Planner(restarted).PlanBatchAsync(Anchor, default));
        Assert.Equal(2, await restarted.ScheduleOccurrences.CountAsync());
        Assert.Equal(0, await Planner(restarted).PlanBatchAsync(Anchor, default));
    }

    [SqlServerFact]
    public async Task Inactivation_during_save_causes_rowversion_conflict_and_preserves_no_new_occurrence()
    {
        await using var database = await TestDatabase.CreateAsync("Maintenance");
        await using var setup = Context(database);
        await setup.Database.MigrateAsync();
        setup.Add(Schedule(1));
        await setup.SaveChangesAsync();
        setup.ChangeTracker.Clear();
        var gate = new SaveGate();
        var options = new DbContextOptionsBuilder<MaintenanceDbContext>()
            .UseSqlServer(database.ConnectionString).AddInterceptors(gate).Options;
        await using var worker = new MaintenanceDbContext(options);
        var planning = Planner(worker).PlanBatchAsync(Anchor, default);
        await gate.Ready.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            var row = await setup.MaintenanceSchedules.SingleAsync();
            row.Revise(Anchor, Anchor, 1, 0, 30, false, Guid.NewGuid(), Anchor);
            await setup.SaveChangesAsync();
        }
        finally { gate.Continue.TrySetResult(); }
        Assert.Equal(0, await planning.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Empty(await worker.ScheduleOccurrences.AsNoTracking().ToListAsync());
        var actual = await worker.MaintenanceSchedules.AsNoTracking().SingleAsync();
        Assert.False(actual.IsActive);
        Assert.Equal(Anchor, actual.NextOccurrenceAtUtc);
    }

    [SqlServerFact]
    public async Task Unique_index_rejects_duplicate_even_without_planner()
    {
        await using var database = await TestDatabase.CreateAsync("Maintenance");
        await using var db = Context(database);
        await db.Database.MigrateAsync();
        db.Add(Schedule(1));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        Assert.Equal(1, await Planner(db).PlanBatchAsync(Anchor, default));
        var duplicate = new ScheduleOccurrence();
        db.Add(duplicate);
        db.Entry(duplicate).CurrentValues.SetValues(new
        {
            ScheduleId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            ScheduledOccurrenceAtUtc = Anchor, DueAtUtc = Anchor.AddMinutes(30), State = "Planned", CreatedAtUtc = Anchor
        });
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.True(error.InnerException is SqlException { Number: 2601 or 2627 });
    }

    private sealed class SaveGate : SaveChangesInterceptor
    {
        public TaskCompletionSource Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Continue { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Ready.TrySetResult();
            await Continue.Task.WaitAsync(cancellationToken);
            return result;
        }
    }
}
