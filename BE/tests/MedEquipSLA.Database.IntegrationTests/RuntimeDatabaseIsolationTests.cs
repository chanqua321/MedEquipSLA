using System.Security.Cryptography;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MedEquipSLA.Database.IntegrationTests;

public sealed class SqlLoginTheoryAttribute : TheoryAttribute
{
    public SqlLoginTheoryAttribute()
    {
        var connection = Environment.GetEnvironmentVariable("SQLSERVER_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection) || new SqlConnectionStringBuilder(connection).IntegratedSecurity)
            Skip = "BLOCKED: SQL-auth SQL Server test instance required to verify runtime login isolation.";
    }
}

public sealed class RuntimeDatabaseIsolationTests
{
    [SqlLoginTheory]
    [InlineData("Identity", "AspNetUsers")]
    [InlineData("Equipment", "EquipmentAssets")]
    [InlineData("Maintenance", "MaintenanceSchedules")]
    [InlineData("Incident", "Incidents")]
    [InlineData("Notification", "Notifications")]
    public async Task Runtime_login_can_read_write_owner_but_cannot_DDL_or_read_other_service(string owner, string table)
    {
        await using var owned = await TestDatabase.CreateAsync(owner);
        var otherOwner = owner == "Equipment" ? "Identity" : "Equipment";
        await using var other = await TestDatabase.CreateAsync(otherOwner);
        await using (var context = Contexts.Create(owner, owned.ConnectionString)) await context.Database.MigrateAsync();
        await using (var context = Contexts.Create(otherOwner, other.ConnectionString)) await context.Database.MigrateAsync();
        var settings = new SqlConnectionStringBuilder(owned.ConnectionString);
        var otherCatalog = new SqlConnectionStringBuilder(other.ConnectionString).InitialCatalog;
        var login = "medequip_test_" + Guid.NewGuid().ToString("N");
        var password = "Aa9!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        var created = false;
        await using var admin = new SqlConnection(owned.ConnectionString); await admin.OpenAsync();
        try
        {
            await using (var create = admin.CreateCommand())
            {
                create.CommandText = """
                    DECLARE @sql nvarchar(max)=N'CREATE LOGIN '+QUOTENAME(@login)+N' WITH PASSWORD='+QUOTENAME(@password,'''')+N', CHECK_POLICY=ON;';
                    EXEC sys.sp_executesql @sql;
                    """;
                create.Parameters.AddWithValue("@login", login); create.Parameters.AddWithValue("@password", password);
                await create.ExecuteNonQueryAsync(); created = true;
            }
            await using (var grant = admin.CreateCommand())
            {
                grant.CommandText = """
                    DECLARE @sql nvarchar(max)=N'CREATE USER '+QUOTENAME(@login)+N' FOR LOGIN '+QUOTENAME(@login)+N'; ALTER ROLE db_datareader ADD MEMBER '+QUOTENAME(@login)+N'; ALTER ROLE db_datawriter ADD MEMBER '+QUOTENAME(@login)+N';';
                    EXEC sys.sp_executesql @sql;
                    """;
                grant.Parameters.AddWithValue("@login", login); await grant.ExecuteNonQueryAsync();
            }
            settings.IntegratedSecurity = false; settings.UserID = login; settings["Password"] = password; settings.Pooling = false;
            await using var runtime = new SqlConnection(settings.ConnectionString); await runtime.OpenAsync();
            await using var command = runtime.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM dbo.[{table}]";
            Assert.Equal(0, Convert.ToInt32(await command.ExecuteScalarAsync()));
            command.CommandText = """
                INSERT dbo.AuditEntries(Id,Action,EntityType,EntityId,OccurredAtUtc,CorrelationId,ChangesJson)
                VALUES(NEWID(),N'RuntimeProbe',N'RuntimeProbe',N'owned',SYSDATETIMEOFFSET(),N'runtime-test',N'{}');
                SELECT COUNT(*) FROM dbo.AuditEntries WHERE Action=N'RuntimeProbe';
                """;
            Assert.Equal(1, Convert.ToInt32(await command.ExecuteScalarAsync()));
            command.CommandText = "CREATE TABLE dbo.ForbiddenRuntimeDdl(Id int NOT NULL)";
            Assert.Equal(262, (await Assert.ThrowsAsync<SqlException>(() => command.ExecuteNonQueryAsync())).Number);
            var otherTable = otherOwner == "Identity" ? "AspNetUsers" : "EquipmentAssets";
            command.CommandText = $"SELECT COUNT(*) FROM [{otherCatalog}].dbo.[{otherTable}]";
            Assert.Equal(916, (await Assert.ThrowsAsync<SqlException>(() => command.ExecuteScalarAsync())).Number);
        }
        finally
        {
            if (created)
            {
                // Login is a fresh GUID recorded only after CREATE succeeded; runtime uses Pooling=false.
                await using var cleanup = admin.CreateCommand();
                cleanup.CommandText = "DECLARE @sql nvarchar(max)=N'DROP USER '+QUOTENAME(@login)+N'; DROP LOGIN '+QUOTENAME(@login)+N';'; EXEC sys.sp_executesql @sql;";
                cleanup.Parameters.AddWithValue("@login", login); await cleanup.ExecuteNonQueryAsync();
            }
        }
    }
}
