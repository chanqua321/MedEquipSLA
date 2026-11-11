using MedEquipSLA.Contracts.Events;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace MedEquipSLA.Database.IntegrationTests;

public sealed class SecurityMigrationGuardTests
{
    [SqlServerTheory]
    [InlineData("Identity", 51072)]
    [InlineData("Maintenance", 51074)]
    [InlineData("Incident", 51074)]
    public async Task Security_history_prevents_loss_on_down_migration(string owner, int expectedError)
    {
        await using var catalog = await TestDatabase.CreateAsync(owner);
        await using var db = Contexts.Create(owner, catalog.ConnectionString); await db.Database.MigrateAsync();
        var id = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        if (db is Identity.Infrastructure.Persistence.IdentityDbContext identity)
            identity.Add(new Identity.Infrastructure.Persistence.Entities.OutboxMessage(new(id, IdentityMailEvent.Requested, 1,
                "Identity", Guid.NewGuid(), 1, now, "migration-guard", "{}")));
        else if (db is Maintenance.Infrastructure.Persistence.MaintenanceDbContext maintenance)
            maintenance.Add(new Maintenance.Infrastructure.Persistence.Entities.InboxMessage("migration-guard", id, UserAccessChangedEvent.Changed, 1, now));
        else ((Incident.Infrastructure.Persistence.IncidentDbContext)db).Add(new Incident.Infrastructure.Persistence.Entities.InboxMessage("migration-guard", id, UserAccessChangedEvent.Changed, 1, now));
        await db.SaveChangesAsync();
        var applied = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
        var guardedIndex = Array.FindIndex(applied, name => name.EndsWith(owner == "Identity" ? "_AddIdentitySecurityOutbox" : "_AddUserAccessInbox", StringComparison.Ordinal));
        Assert.True(guardedIndex > 0);
        var error = await Assert.ThrowsAsync<SqlException>(() => db.GetService<IMigrator>().MigrateAsync(applied[guardedIndex - 1]));
        Assert.Equal(expectedError, error.Number);
        Assert.Equal(applied.Take(guardedIndex + 1), await db.Database.GetAppliedMigrationsAsync());
        var table = owner == "Identity" ? "OutboxMessages" : "InboxMessages";
        await using var connection = new SqlConnection(catalog.ConnectionString); await connection.OpenAsync();
        await using var command = connection.CreateCommand(); command.CommandText = $"SELECT COUNT(*) FROM dbo.{table}";
        Assert.Equal(1, Convert.ToInt32(await command.ExecuteScalarAsync()));
    }
}
