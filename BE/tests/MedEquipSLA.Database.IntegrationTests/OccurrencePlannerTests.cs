using MedEquipSLA.Maintenance.Domain;
using MedEquipSLA.Maintenance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MedEquipSLA.Database.IntegrationTests;

public sealed class OccurrencePlannerTests
{
    private static OccurrencePlanner Planner(MaintenanceDbContext db) => new(db, new OccurrencePlanningCursor(),
        Options.Create(new OccurrencePlanningOptions()), NullLogger<OccurrencePlanner>.Instance);

    [SqlServerFact]
    public async Task Plans_once_at_reminder_boundary_and_catches_up_from_original_time()
    {
        await using var database = await TestDatabase.CreateAsync("Maintenance");
        await using var first = (MaintenanceDbContext)Contexts.Create("Maintenance", database.ConnectionString);
        await using var second = (MaintenanceDbContext)Contexts.Create("Maintenance", database.ConnectionString);
        await first.Database.MigrateAsync();

        var scheduled = new DateTimeOffset(2026, 11, 10, 10, 0, 0, TimeSpan.Zero);
        var schedule = new MaintenanceSchedule();
        first.Add(schedule);
        first.Entry(schedule).CurrentValues.SetValues(new
        {
            Code = "CAL-TEST", EquipmentId = Guid.NewGuid(), EquipmentCodeSnapshot = "EQ-1",
            EquipmentNameSnapshot = "Monitor", DepartmentIdSnapshot = Guid.NewGuid(), DepartmentNameSnapshot = "ICU",
            WorkType = "Calibration", AnchorAtUtc = scheduled, NextOccurrenceAtUtc = scheduled, IntervalDays = 7,
            ReminderLeadMinutes = 1440, CompletionWindowMinutes = 30, EquipmentUseRegistrationId = Guid.NewGuid(),
            CreatedAtUtc = scheduled.AddDays(-10), UpdatedAtUtc = scheduled.AddDays(-10)
        });
        await first.SaveChangesAsync();
        first.ChangeTracker.Clear();

        var planner = Planner(first);
        Assert.Equal(0, await planner.PlanBatchAsync(scheduled.AddDays(-1).AddSeconds(-1), default));
        var parallel = await Task.WhenAll(planner.PlanBatchAsync(scheduled.AddDays(-1), default),
            Planner(second).PlanBatchAsync(scheduled.AddDays(-1), default));
        Assert.Equal(1, parallel.Sum());
        Assert.Equal(0, await planner.PlanBatchAsync(scheduled.AddDays(-1), default));
        Assert.Equal(1, await planner.PlanBatchAsync(scheduled.AddDays(8), default));

        var occurrences = await first.ScheduleOccurrences.OrderBy(x => x.ScheduledOccurrenceAtUtc).ToListAsync();
        Assert.Collection(occurrences, initial =>
        {
            Assert.Equal(scheduled, initial.ScheduledOccurrenceAtUtc);
            Assert.Equal(scheduled.AddMinutes(30), initial.DueAtUtc);
            Assert.Equal("Planned", initial.State);
        }, missed => Assert.Equal(scheduled.AddDays(7), missed.ScheduledOccurrenceAtUtc));
        Assert.Empty(await first.WorkOrders.ToListAsync());
    }
}
