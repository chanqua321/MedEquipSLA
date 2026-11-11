using MedEquipSLA.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace MedEquipSLA.Database.IntegrationTests;

public sealed class DevelopmentMigrationTests
{
    private static WebApplication CreateApp(string environment, bool enabled, string? connection = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment, Args = [] });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Migrations:ApplyOnStartup"] = enabled.ToString(),
            ["ConnectionStrings:Database"] = connection
        });
        return builder.Build();
    }

    [Theory]
    [InlineData("Production", true)]
    [InlineData("Staging", true)]
    [InlineData("Development", false)]
    public async Task Migration_callback_is_not_called_outside_opted_in_development(string environment, bool enabled)
    {
        await using var app = CreateApp(environment, enabled);
        var called = false;
        await app.ApplyDevelopmentMigrationsAsync("Equipment", _ => { called = true; return Task.CompletedTask; });
        Assert.False(called);
    }

    [Fact]
    public async Task Migration_failure_stops_startup_without_exposing_connection_secrets()
    {
        await using var app = CreateApp("Development", true);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            app.ApplyDevelopmentMigrationsAsync("Equipment", _ => throw new Exception("secret-value-from-provider")));
        Assert.DoesNotContain("secret-value-from-provider", error.ToString());
        Assert.Null(error.InnerException);
    }

    [Theory]
    [MemberData(nameof(SchemaAndMigrationTests.Owners), MemberType = typeof(SchemaAndMigrationTests))]
    public async Task Startup_migration_rejects_another_services_database(string owner)
    {
        await using var app = CreateApp("Development", true, "Server=localhost,14333;Database=UnrelatedDatabase;Integrated Security=True");
        // Catalog guard fails before trying to connect to SQL Server.
        await Assert.ThrowsAsync<InvalidOperationException>(() => app.ApplyDevelopmentMigrationsAsync(owner, ct => Apply(owner, app.Configuration, ct)));
    }

    [SqlServerTheory]
    [MemberData(nameof(SchemaAndMigrationTests.Owners), MemberType = typeof(SchemaAndMigrationTests))]
    public async Task Development_startup_applies_pending_migrations_and_is_safe_on_restart(string owner)
    {
        await using var database = await TestDatabase.CreateAsync(owner);
        await using var app = CreateApp("Development", true, database.ConnectionString);
        await app.ApplyDevelopmentMigrationsAsync(owner, ct => Apply(owner, app.Configuration, ct));
        await app.ApplyDevelopmentMigrationsAsync(owner, ct => Apply(owner, app.Configuration, ct));
        await using var verification = Contexts.Create(owner, database.ConnectionString);
        Assert.Equal(verification.Database.GetMigrations(), await verification.Database.GetAppliedMigrationsAsync());
        Assert.Empty(await verification.Database.GetPendingMigrationsAsync());
    }

    private static Task Apply(string owner, IConfiguration configuration, CancellationToken ct) => owner switch
    {
        "Identity" => Identity.Infrastructure.Persistence.IdentityDatabaseMigration.ApplyAsync(configuration, ct),
        "Equipment" => Equipment.Infrastructure.Persistence.EquipmentDatabaseMigration.ApplyAsync(configuration, ct),
        "Maintenance" => Maintenance.Infrastructure.Persistence.MaintenanceDatabaseMigration.ApplyAsync(configuration, ct),
        "Incident" => Incident.Infrastructure.Persistence.IncidentDatabaseMigration.ApplyAsync(configuration, ct),
        "Notification" => Notification.Infrastructure.Persistence.NotificationDatabaseMigration.ApplyAsync(configuration, ct),
        _ => throw new ArgumentOutOfRangeException(nameof(owner))
    };
}
