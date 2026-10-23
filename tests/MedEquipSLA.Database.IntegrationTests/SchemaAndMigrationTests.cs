using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace MedEquipSLA.Database.IntegrationTests;

public sealed class SchemaAndMigrationTests
{
    public static IEnumerable<object[]> Owners => Contexts.Owners.Select(owner => new object[] { owner });

    [Theory]
    [MemberData(nameof(Owners))]
    public void Ef_model_matches_approved_erd_and_database_ownership(string owner)
    {
        using var context = Contexts.Create(owner, $"Server=localhost,14333;Database=MedEquip_{owner}_Test_Model;Integrated Security=True;TrustServerCertificate=True");
        var model = context.GetService<IDesignTimeModel>().Model;
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "erd-model.json")));
        var root = document.RootElement;
        Assert.Equal("APPROVED_FOR_IMPLEMENTATION", root.GetProperty("status").GetString());
        var schema = root.GetProperty("schemas").EnumerateArray().Single(s => s.GetProperty("service").GetString() == owner);
        var expected = schema.GetProperty("tables").EnumerateArray().ToList();
        foreach (var technical in root.GetProperty("technicalOwnership").GetProperty(owner).EnumerateArray())
            expected.Add(root.GetProperty("technicalTables").EnumerateArray().Single(t => t.GetProperty("name").GetString() == technical.GetString()));
        Assert.Equal(expected.Count, model.GetEntityTypes().Count());
        foreach (var table in expected)
        {
            var entity = model.GetEntityTypes().Single(e => e.GetTableName() == table.GetProperty("name").GetString());
            var columns = table.GetProperty("columns").EnumerateArray().ToArray();
            Assert.Equal(columns.Length, entity.GetProperties().Count());
            foreach (var column in columns)
            {
                var property = entity.FindProperty(column.GetProperty("name").GetString()!)!;
                Assert.NotNull(property);
                Assert.False(property.IsShadowProperty());
                Assert.Equal(column.GetProperty("type").GetString(), property.GetColumnType());
                Assert.Equal(column.GetProperty("nullable").GetBoolean(), property.IsNullable);
                if (property.Name == "RowVersion")
                {
                    Assert.True(property.IsConcurrencyToken);
                    Assert.Equal(ValueGenerated.OnAddOrUpdate, property.ValueGenerated);
                }
            }
            var key = columns.Where(c => c.GetProperty("key").GetString()!.Contains("PK", StringComparison.Ordinal))
                .Select(c => c.GetProperty("name").GetString()).ToArray();
            Assert.Equal(key, entity.FindPrimaryKey()!.Properties.Select(p => p.Name));
            foreach (var fk in entity.GetForeignKeys())
            {
                Assert.Equal(DeleteBehavior.NoAction, fk.DeleteBehavior);
                Assert.Contains(expected, t => t.GetProperty("name").GetString() == fk.PrincipalEntityType.GetTableName());
            }
        }
        Assert.False(context.Database.HasPendingModelChanges());
        Assert.Single(context.Database.GetMigrations());
    }

    [SqlServerTheory]
    [MemberData(nameof(Owners))]
    public async Task InitialCreate_schema_constraints_rollback_and_reapply_are_valid(string owner)
    {
        await using var database = await TestDatabase.CreateAsync(owner);
        await using var context = Contexts.Create(owner, database.ConnectionString);
        await context.Database.MigrateAsync();
        Assert.Single(await context.Database.GetAppliedMigrationsAsync());
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        await RunSqlSuitesAsync(owner, database.ConnectionString);
        await AssertIndexesAsync(context, database.ConnectionString);
        await AssertCheckConstraintsAsync(context, database.ConnectionString);
        await context.GetService<IMigrator>().MigrateAsync("0");
        Assert.Empty(await context.Database.GetAppliedMigrationsAsync());
        await using (var connection = new SqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0 AND name<>'__EFMigrationsHistory'";
            Assert.Equal(0, Convert.ToInt32(await command.ExecuteScalarAsync()));
        }
        await context.Database.MigrateAsync();
        Assert.Single(await context.Database.GetAppliedMigrationsAsync());
        await RunSqlSuitesAsync(owner, database.ConnectionString);
    }

    private static async Task RunSqlSuitesAsync(string owner, string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        foreach (var suite in new[] { "Schema", "Behavior" })
        {
            await using var command = connection.CreateCommand();
            command.CommandText = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Sql", $"{owner}-{suite}.sql"));
            command.CommandTimeout = 60;
            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task AssertIndexesAsync(DbContext context, string connectionString)
    {
        var model = context.GetService<IDesignTimeModel>().Model;
        var expected = model.GetEntityTypes().SelectMany(e => e.GetIndexes().Select(i => new
        {
            Table = e.GetTableName(), Name = i.GetDatabaseName(), Unique = i.IsUnique,
            Filter = Normalize(i.GetFilter()), Columns = string.Join(',', i.Properties.Select(p => p.Name))
        })).ToArray();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT t.name,i.name,i.is_unique,i.filter_definition,
                STRING_AGG(CONVERT(nvarchar(max),c.name),',') WITHIN GROUP(ORDER BY ic.key_ordinal)
            FROM sys.tables t JOIN sys.indexes i ON t.object_id=i.object_id
            JOIN sys.index_columns ic ON i.object_id=ic.object_id AND i.index_id=ic.index_id
            JOIN sys.columns c ON ic.object_id=c.object_id AND ic.column_id=c.column_id
            WHERE t.is_ms_shipped=0 AND t.name<>'__EFMigrationsHistory' AND i.is_primary_key=0 AND ic.key_ordinal>0
            GROUP BY t.name,i.name,i.is_unique,i.filter_definition
            """;
        await using var reader = await command.ExecuteReaderAsync();
        var actualCount = 0;
        while (await reader.ReadAsync())
        {
            actualCount++;
            var index = Assert.Single(expected, i => i.Table == reader.GetString(0) && i.Name == reader.GetString(1));
            Assert.Equal(index.Unique, reader.GetBoolean(2));
            Assert.Equal(index.Filter, Normalize(reader.IsDBNull(3) ? null : reader.GetString(3)));
            Assert.Equal(index.Columns, reader.GetString(4));
        }
        Assert.Equal(expected.Length, actualCount);
    }

    private static async Task AssertCheckConstraintsAsync(DbContext context, string connectionString)
    {
        var model = context.GetService<IDesignTimeModel>().Model;
        var expected = model.GetEntityTypes().SelectMany(e => e.GetCheckConstraints().Select(c => new
        {
            Table = e.GetTableName(), c.Name
        })).ToArray();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT OBJECT_NAME(parent_object_id),name,definition,is_disabled,is_not_trusted FROM sys.check_constraints";
        await using var reader = await command.ExecuteReaderAsync();
        var actualCount = 0;
        while (await reader.ReadAsync())
        {
            actualCount++;
            Assert.Single(expected, c => c.Table == reader.GetString(0) && c.Name == reader.GetString(1));
            // SQL Server rewrites IN to OR and can reorder terms. Validate identity,
            // enabled/trusted metadata and the negative behavior suites, not SQL text spelling.
            Assert.False(string.IsNullOrWhiteSpace(reader.GetString(2)));
            Assert.False(reader.GetBoolean(3));
            Assert.False(reader.GetBoolean(4));
        }
        Assert.Equal(expected.Length, actualCount);
    }

    private static string Normalize(string? sql) => Regex.Replace(sql ?? "", @"[\s()\[\]]", "").ToLowerInvariant();
}
