using System.Text.Json;
using MedEquipSLA.Contracts.Events;
using Microsoft.EntityFrameworkCore;
using Xunit;
using MaintenanceDb = MedEquipSLA.Maintenance.Infrastructure.Persistence.MaintenanceDbContext;
using IncidentDb = MedEquipSLA.Incident.Infrastructure.Persistence.IncidentDbContext;

namespace MedEquipSLA.Database.IntegrationTests;

public sealed class UserAccessProjectionTests
{
    [SqlServerTheory]
    [InlineData("Maintenance")]
    [InlineData("Incident")]
    public async Task Revocation_is_atomic_idempotent_preserves_lifecycle_and_does_not_remove_newer_assignment(string owner)
    {
        await using var catalog = await TestDatabase.CreateAsync(owner);
        await using var db = Contexts.Create(owner, catalog.ConnectionString); await db.Database.MigrateAsync();
        var tables = owner == "Maintenance" ? new[] { "MaintenanceSchedules", "ScheduleOccurrences", "WorkOrders" } :
            new[] { "SlaPolicies", "SlaPolicyVersions", "Incidents" };
        var fixture = await File.ReadAllLinesAsync(Path.Combine(AppContext.BaseDirectory, "Sql", owner + "-Behavior.sql"));
        foreach (var table in tables)
            await db.Database.ExecuteSqlRawAsync(fixture.First(x => x.StartsWith($"INSERT INTO [dbo].[{table}]", StringComparison.Ordinal)));
        var technician = Guid.NewGuid(); var actor = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        string status; DateTimeOffset deadline; Guid id;
        if (db is MaintenanceDb maintenance)
        {
            var row = await maintenance.WorkOrders.SingleAsync(); row.Assign(technician, actor, now, 1);
            status = row.Status; deadline = row.DueAtUtc; id = row.Id;
        }
        else
        {
            var row = await ((IncidentDb)db).Incidents.SingleAsync(); row.Assign(technician, actor, now, 1);
            status = row.Status; deadline = row.ResolutionDeadlineAtUtc; id = row.Id;
        }
        await db.SaveChangesAsync();
        var envelope = new IntegrationEvent(Guid.NewGuid(), UserAccessChangedEvent.Changed, 1, "Identity", technician, 2, now,
            "access-projection-test", JsonSerializer.Serialize(new UserAccessChangedEvent(technician, 2)));
        async Task Apply(IntegrationEvent message, bool eligible, long version, bool inbox = true)
        {
            if (db is MaintenanceDb m) await Maintenance.Infrastructure.Workers.UserAccessConsumer.ApplyAsync(m, message, eligible, version, default, inbox);
            else await Incident.Infrastructure.Workers.UserAccessConsumer.ApplyAsync((IncidentDb)db, message, eligible, version, default, inbox);
        }
        await Apply(envelope, false, 2); await Apply(envelope, false, 2);
        async Task Check(Guid? expected)
        {
            db.ChangeTracker.Clear();
            if (db is MaintenanceDb m)
            {
                var row = await m.WorkOrders.SingleAsync(); Assert.Equal(expected, row.AssignedTechnicianId);
                Assert.Equal(status, row.Status); Assert.Equal(deadline, row.DueAtUtc); Assert.Equal(id, row.Id);
            }
            else
            {
                var row = await ((IncidentDb)db).Incidents.SingleAsync(); Assert.Equal(expected, row.AssignedTechnicianId);
                Assert.Equal(status, row.Status); Assert.Equal(deadline, row.ResolutionDeadlineAtUtc); Assert.Equal(id, row.Id);
            }
        }
        await Check(null);
        if (db is MaintenanceDb md) (await md.WorkOrders.SingleAsync()).Assign(technician, actor, now, 4);
        else (await ((IncidentDb)db).Incidents.SingleAsync()).Assign(technician, actor, now, 4);
        await db.SaveChangesAsync();
        // A stale lookup/event cannot undo a re-admission verified at a newer Identity version.
        await Apply(envelope with { EventId = Guid.NewGuid() }, false, 3); await Check(technician);
        await Apply(envelope with { EventId = Guid.NewGuid() }, true, 5); await Check(technician);
        // Reconciliation repairs a commit which happened after the original event was consumed.
        await Apply(envelope, false, 5, inbox: false); await Check(null);
        if (db is MaintenanceDb mc)
        {
            Assert.Equal(2, await mc.WorkOrderActivities.CountAsync(x => x.Action == "AssignmentRevoked"));
            Assert.Equal(3, await mc.InboxMessages.CountAsync()); Assert.Equal(2, await mc.OutboxMessages.CountAsync());
        }
        else
        {
            var ic = (IncidentDb)db; Assert.Equal(2, await ic.IncidentActivities.CountAsync(x => x.Action == "AssignmentRevoked"));
            Assert.Equal(3, await ic.InboxMessages.CountAsync()); Assert.Equal(2, await ic.OutboxMessages.CountAsync());
        }
    }
}
