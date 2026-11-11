using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace MedEquipSLA.Identity.Infrastructure.Persistence;

public static class IdentityDatabaseMigration
{
    public static async Task ApplyAsync(IConfiguration configuration, CancellationToken cancellationToken)
    {
        var connection = configuration.GetConnectionString("Migration");
        var runtimeConnection = configuration.GetConnectionString("Database");
        if (!string.IsNullOrWhiteSpace(connection) && !string.IsNullOrWhiteSpace(runtimeConnection))
        {
            var migrationTarget = new SqlConnectionStringBuilder(connection);
            var runtimeTarget = new SqlConnectionStringBuilder(runtimeConnection);
            if (!string.Equals(migrationTarget.DataSource, runtimeTarget.DataSource, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(migrationTarget.InitialCatalog, runtimeTarget.InitialCatalog, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Migration and runtime connections must target the same development database.");
        }
        if (string.IsNullOrWhiteSpace(connection))
            connection = runtimeConnection;
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("Configure a development database connection before applying migrations.");
        await using var context = IdentityDesignTimeFactory.CreateForConnection(connection);
        await context.Database.MigrateAsync(cancellationToken);
    }
}
