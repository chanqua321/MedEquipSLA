using MedEquipSLA.Maintenance.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace MedEquipSLA.Database.IntegrationTests;

public sealed class ReminderWindowMigrationTests
{
    [SqlServerFact]
    public async Task Upgrade_and_rollback_preserve_legacy_occurrence_times_without_guessing_window()
    {
        await using var database = await TestDatabase.CreateAsync("Maintenance");
        await using var db = (MaintenanceDbContext)Contexts.Create("Maintenance", database.ConnectionString);
        const string initial = "20261023044400_InitialCreate";
        await db.GetService<IMigrator>().MigrateAsync(initial);
        var fixture = await File.ReadAllLinesAsync(Path.Combine(AppContext.BaseDirectory, "Sql", "Maintenance-Behavior.sql"));
        var rows = fixture.Where(x => x.StartsWith("INSERT ", StringComparison.Ordinal)).Take(2);
        await db.Database.ExecuteSqlRawAsync(string.Join(Environment.NewLine, rows));
        var before = await TimesAsync(database.ConnectionString);
        await db.Database.MigrateAsync();
        var occurrence = await db.ScheduleOccurrences.AsNoTracking().SingleAsync();
        Assert.Null(occurrence.ReminderWindowStartAtUtc);
        Assert.Equal(before.Scheduled, occurrence.ScheduledOccurrenceAtUtc);
        Assert.Equal(before.Due, occurrence.DueAtUtc);
        await db.GetService<IMigrator>().MigrateAsync(initial);
        Assert.Equal(before, await TimesAsync(database.ConnectionString));
        await db.Database.MigrateAsync();
        Assert.Null((await db.ScheduleOccurrences.AsNoTracking().SingleAsync()).ReminderWindowStartAtUtc);
    }

    private static async Task<(DateTimeOffset Scheduled, DateTimeOffset Due)> TimesAsync(string connection)
    {
        await using var sql = new SqlConnection(connection);
        await sql.OpenAsync();
        await using var command = sql.CreateCommand();
        command.CommandText = "SELECT ScheduledOccurrenceAtUtc,DueAtUtc FROM dbo.ScheduleOccurrences";
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return (reader.GetDateTimeOffset(0), reader.GetDateTimeOffset(1));
    }
}
