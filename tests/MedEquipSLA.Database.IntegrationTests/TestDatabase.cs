using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MedEquipSLA.Database.IntegrationTests;

// SQL tests are opt-in and explicitly skipped, never reported as passing without SQL.
public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SQLSERVER_TEST_CONNECTION")))
            Skip = "BLOCKED: SQLSERVER_TEST_CONNECTION is not configured; real SQL Server required.";
    }
}

public sealed class SqlServerTheoryAttribute : TheoryAttribute
{
    public SqlServerTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SQLSERVER_TEST_CONNECTION")))
            Skip = "BLOCKED: SQLSERVER_TEST_CONNECTION is not configured; real SQL Server required.";
    }
}

internal static class Contexts
{
    public static readonly string[] Owners = ["Identity", "Equipment", "Maintenance", "Incident", "Notification"];

    private static DbContextOptions<T> Options<T>(string connection) where T : DbContext =>
        new DbContextOptionsBuilder<T>().UseSqlServer(connection, sql => sql.UseCompatibilityLevel(160)).Options;

    public static DbContext Create(string owner, string connection) => owner switch
    {
        "Identity" => new Identity.Infrastructure.Persistence.IdentityDbContext(Options<Identity.Infrastructure.Persistence.IdentityDbContext>(connection)),
        "Equipment" => new Equipment.Infrastructure.Persistence.EquipmentDbContext(Options<Equipment.Infrastructure.Persistence.EquipmentDbContext>(connection)),
        "Maintenance" => new Maintenance.Infrastructure.Persistence.MaintenanceDbContext(Options<Maintenance.Infrastructure.Persistence.MaintenanceDbContext>(connection)),
        "Incident" => new Incident.Infrastructure.Persistence.IncidentDbContext(Options<Incident.Infrastructure.Persistence.IncidentDbContext>(connection)),
        "Notification" => new Notification.Infrastructure.Persistence.NotificationDbContext(Options<Notification.Infrastructure.Persistence.NotificationDbContext>(connection)),
        _ => throw new ArgumentOutOfRangeException(nameof(owner))
    };
}

internal sealed class TestDatabase : IAsyncDisposable
{
    private readonly string masterConnection;
    private readonly string ownedName;
    private bool created;
    public string ConnectionString { get; }

    private TestDatabase(string owner)
    {
        if (!Contexts.Owners.Contains(owner)) throw new ArgumentOutOfRangeException(nameof(owner));
        var settings = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("SQLSERVER_TEST_CONNECTION"));
        var server = settings.DataSource.ToLowerInvariant();
        if (server is not ("(localdb)\\mssqllocaldb" or "localhost,14333" or "127.0.0.1,14333" or "tcp:localhost,14333" or "tcp:127.0.0.1,14333"))
            throw new InvalidOperationException("Tests accept only LocalDB or loopback SQL Server port 14333; remote/production targets are rejected.");
        if (!string.IsNullOrEmpty(settings.InitialCatalog) && settings.InitialCatalog != "master")
            throw new InvalidOperationException("Test base connection must use master or omit Database; existing application catalogs are rejected.");
        settings.InitialCatalog = "master";
        masterConnection = settings.ConnectionString;
        ownedName = $"MedEquip_{owner}_Test_{Guid.NewGuid():N}";
        settings.InitialCatalog = ownedName;
        ConnectionString = settings.ConnectionString;
    }

    public static async Task<TestDatabase> CreateAsync(string owner)
    {
        var database = new TestDatabase(owner);
        await using var connection = new SqlConnection(database.masterConnection);
        await connection.OpenAsync();
        // CREATE fails on collision. Ownership is recorded only after it succeeds;
        // cleanup can therefore never delete a pre-existing database.
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE [{database.ownedName}]";
        await command.ExecuteNonQueryAsync();
        database.created = true;
        try
        {
            command.CommandText = $"ALTER DATABASE [{database.ownedName}] SET COMPATIBILITY_LEVEL = 160";
            await command.ExecuteNonQueryAsync();
            return database;
        }
        catch
        {
            await database.DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!created) return;
        var catalog = new SqlConnectionStringBuilder(ConnectionString).InitialCatalog;
        if (catalog != ownedName || !System.Text.RegularExpressions.Regex.IsMatch(ownedName,
                "^MedEquip_(Identity|Equipment|Maintenance|Incident|Notification)_Test_[0-9a-f]{32}$"))
            throw new InvalidOperationException("Refusing cleanup of a database not owned by this fixture.");
        SqlConnection.ClearAllPools();
        await using var connection = new SqlConnection(masterConnection);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"ALTER DATABASE [{ownedName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{ownedName}]";
        await command.ExecuteNonQueryAsync();
        created = false;
    }
}
