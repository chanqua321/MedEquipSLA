using MedEquipSLA.Equipment.Domain;
using MedEquipSLA.Equipment.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;
using static MedEquipSLA.Database.IntegrationTests.EquipmentRetirementDomainTests;

namespace MedEquipSLA.Database.IntegrationTests;

public sealed class EquipmentRetirementSqlTests
{
    internal const string Initial = "20261023044354_InitialCreate";
    internal const string Latest = "20261106063402_AddEquipmentRetirementFoundation";

    internal static async Task<EquipmentAsset> SeedAsync(EquipmentDbContext db)
    {
        var department = Department.Create("DEP-" + Guid.NewGuid().ToString("N"), "Unit", Actor, Now);
        var category = EquipmentCategory.Create("CAT-" + Guid.NewGuid().ToString("N"), "Category", Actor, Now);
        var asset = EquipmentAsset.Register("EQ-" + Guid.NewGuid().ToString("N"), department.Id,
            new("Device", null, category.Id, null, null, null, null), Actor, Now);
        db.AddRange(department, category, asset);
        await db.SaveChangesAsync();
        return asset;
    }

    [SqlServerFact]
    public async Task Sql_active_intent_epoch_and_receipt_unique_constraints_and_rowversion_are_real()
    {
        await using var database = await TestDatabase.CreateAsync("Equipment");
        await using var db = (EquipmentDbContext)Contexts.Create("Equipment", database.ConnectionString);
        await db.Database.MigrateAsync();
        var asset = await SeedAsync(db);
        var first = Begin(asset);
        db.EquipmentRetirementIntents.Add(first);
        await db.SaveChangesAsync();
        var version = first.RowVersion.ToArray();
        // Deliberately stale history simulates another transaction: SQL must enforce the global invariant.
        var duplicate = Begin(asset);
        db.Add(duplicate);
        var activeError = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Contains(((SqlException)activeError.InnerException!).Number, new[] { 2601, 2627 });
        db.Entry(duplicate).State = EntityState.Detached;
        first.Continue(Actor, "Check again", Now.AddMinutes(1));
        await db.SaveChangesAsync();
        Assert.False(version.SequenceEqual(first.RowVersion));
        Report(first, "Maintenance", "Frozen");
        await db.SaveChangesAsync();
        var receipt = first.Receipts.Single();
        var receiptVersion = receipt.RowVersion.ToArray();
        Report(first, "Maintenance", "Sealed", 2);
        await db.SaveChangesAsync();
        Assert.False(receiptVersion.SequenceEqual(receipt.RowVersion));
        await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT dbo.RetirementSourceReceipts
            (IntentId,SourceService,EquipmentId,RetirementEpoch,CoverageVersion,SourceGateVersion,ReadyProofId,SourceState,ReceivedAtUtc)
            SELECT IntentId,SourceService,EquipmentId,RetirementEpoch,CoverageVersion,SourceGateVersion,ReadyProofId,SourceState,ReceivedAtUtc
            FROM dbo.RetirementSourceReceipts WHERE IntentId={first.Id}
            """));
        first.RequestCancel(Actor, "Cancel", Now.AddMinutes(2));
        Report(first, "Maintenance", "CancelAck", 3);
        Report(first, "Incident", "CancelAck", 1);
        first.ConfirmCancelled(Now.AddMinutes(2));
        await db.SaveChangesAsync();
        duplicate = Begin(asset); // epoch 1 may not be reused even though old intent is terminal.
        db.Add(duplicate);
        var epochError = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Contains(((SqlException)epochError.InnerException!).Number, new[] { 2601, 2627 });
        db.Entry(duplicate).State = EntityState.Detached;
        var next = Begin(asset, first);
        db.Add(next);
        await db.SaveChangesAsync();
        Assert.Equal(2, next.RetirementEpoch);
        db.ChangeTracker.Clear();
        var loaded = await db.EquipmentRetirementIntents.Include(x => x.Receipts).SingleAsync(x => x.Id == first.Id);
        Assert.Equal("Cancelled", loaded.State);
        Assert.Equal(2, loaded.Receipts.Count);
        Assert.NotNull(loaded.Receipts.Single(x => x.SourceService == "Maintenance").ReadyProofId);
    }

    [SqlServerFact]
    public async Task Equipment_lock_serializes_two_epoch_allocators_with_no_second_active_intent()
    {
        await using var database = await TestDatabase.CreateAsync("Equipment");
        Guid assetId;
        await using (var db = (EquipmentDbContext)Contexts.Create("Equipment", database.ConnectionString))
        { await db.Database.MigrateAsync(); assetId = (await SeedAsync(db)).Id; }
        async Task<bool> AllocateAsync()
        {
            await using var db = (EquipmentDbContext)Contexts.Create("Equipment", database.ConnectionString);
            await using var tx = await db.Database.BeginTransactionAsync();
            var asset = await db.EquipmentAssets.FromSqlInterpolated(
                $"SELECT * FROM dbo.EquipmentAssets WITH (UPDLOCK,HOLDLOCK) WHERE Id={assetId}").SingleAsync();
            var history = await db.EquipmentRetirementIntents.Where(x => x.EquipmentId == assetId).ToListAsync();
            if (history.Any(x => x.IsActive)) return false;
            db.Add(EquipmentRetirementIntent.Begin(asset, history, Actor, Guid.NewGuid(), "Retire", Now));
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return true;
        }
        var result = await Task.WhenAll(AllocateAsync(), AllocateAsync());
        Assert.Single(result, x => x);
        await using var verify = (EquipmentDbContext)Contexts.Create("Equipment", database.ConnectionString);
        Assert.Single(await verify.EquipmentRetirementIntents.ToListAsync());
    }

    [SqlServerFact]
    public async Task Sql_constraints_reject_wrong_receipt_binding_missing_proof_invalid_json_and_cascade_delete()
    {
        await using var database = await TestDatabase.CreateAsync("Equipment");
        await using var db = (EquipmentDbContext)Contexts.Create("Equipment", database.ConnectionString);
        await db.Database.MigrateAsync();
        var asset = await SeedAsync(db);
        var intent = Begin(asset);
        Report(intent, "Maintenance", "Frozen");
        db.Add(intent);
        await db.SaveChangesAsync();
        async Task Rejected(FormattableString sql)
        {
            var e = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(sql));
            Assert.Equal(547, e.Number);
        }
        await Rejected($"UPDATE dbo.RetirementSourceReceipts SET RetirementEpoch=2 WHERE IntentId={intent.Id}");
        await Rejected($"UPDATE dbo.RetirementSourceReceipts SET SourceState=N'Sealed' WHERE IntentId={intent.Id}");
        await Rejected($"UPDATE dbo.RetirementSourceReceipts SET CoverageVersion=2 WHERE IntentId={intent.Id}");
        await Rejected($"UPDATE dbo.RetirementSourceReceipts SET SourceService=N'Other' WHERE IntentId={intent.Id}");
        await Rejected($"UPDATE dbo.EquipmentRetirementIntents SET State=N'Cancelling' WHERE Id={intent.Id}");
        await Rejected($"UPDATE dbo.EquipmentRetirementIntents SET RetirementEpoch=0 WHERE Id={intent.Id}");
        await Rejected($"DELETE dbo.EquipmentRetirementIntents WHERE Id={intent.Id}");
        await Rejected($"DELETE dbo.EquipmentAssets WHERE Id={asset.Id}");
        var request = new WorkOrderRegistrationRequest(asset.Id, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Maintenance", Now, Now.AddHours(1));
        var registration = EquipmentUseRegistration.RegisterWorkOrder(asset, request, Guid.NewGuid(), 1, "Unit", Now);
        db.Add(registration);
        await db.SaveChangesAsync();
        await Rejected($"UPDATE dbo.EquipmentUseRegistrations SET RequestPayloadJson=N'not-json' WHERE Id={registration.Id}");
        await Rejected($"UPDATE dbo.EquipmentUseRegistrations SET FencingToken=-1 WHERE Id={registration.Id}");
        db.ChangeTracker.Clear();
        var read = await db.EquipmentUseRegistrations.SingleAsync();
        Assert.Equal(request.Hash(), read.RequestHash);
        Assert.Equal(32, read.RequestHash!.Length);
        Assert.Equal("Device", read.EquipmentNameSnapshot);
        await using var connection = new SqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT TYPE_NAME(user_type_id),max_length FROM sys.columns WHERE object_id=OBJECT_ID('dbo.EquipmentUseRegistrations') AND name='RequestHash'";
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("binary", reader.GetString(0));
        Assert.Equal(32, reader.GetInt16(1));
    }

    [SqlServerFact]
    public async Task Legacy_upgrade_down_reapply_preserves_rows_and_existing_registration_uniques()
    {
        await using var database = await TestDatabase.CreateAsync("Equipment");
        await using var db = (EquipmentDbContext)Contexts.Create("Equipment", database.ConnectionString);
        await db.GetService<IMigrator>().MigrateAsync(Initial);
        // Use the old-schema INSERT fixture without querying the new EF model against old columns.
        var fixture = await File.ReadAllLinesAsync(Path.Combine(AppContext.BaseDirectory, "Sql", "Equipment-Behavior.sql"));
        await db.Database.ExecuteSqlRawAsync(string.Join(Environment.NewLine,
            fixture.Where(x => x.StartsWith("INSERT ", StringComparison.Ordinal)).Take(3)));
        var resource = Guid.NewGuid();
        var operation = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT dbo.EquipmentUseRegistrations
            (Id,EquipmentId,SourceService,ResourceType,ResourceId,SourceOperationId,State,SourceVersion,RegisteredAtUtc)
            SELECT {Guid.NewGuid()},Id,N'Maintenance',N'MaintenanceSchedule',{resource},{operation},N'Pending',0,{Now}
            FROM dbo.EquipmentAssets
            """);
        async Task<string> LegacySnapshot()
        {
            await using var c = new SqlConnection(database.ConnectionString);
            await c.OpenAsync();
            await using var q = c.CreateCommand();
            q.CommandText = """
                SELECT Id,EquipmentId,SourceService,ResourceType,ResourceId,SourceOperationId,State,SourceVersion,
                  RegisteredAtUtc,ConfirmedAtUtc,ReleasedAtUtc,LastVerifiedAtUtc,RowVersion
                FROM dbo.EquipmentUseRegistrations FOR JSON PATH,INCLUDE_NULL_VALUES
                """;
            return (string)(await q.ExecuteScalarAsync())!;
        }
        var before = await LegacySnapshot();
        await db.Database.MigrateAsync();
        Assert.Equal(before, await LegacySnapshot());
        var legacy = await db.EquipmentUseRegistrations.AsNoTracking().SingleAsync();
        Assert.Null(legacy.RequestHash);
        Assert.Null(legacy.RequestPayloadJson);
        Assert.Null(legacy.EquipmentNameSnapshot);
        Assert.Equal(0, legacy.FencingToken);
        await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT dbo.EquipmentUseRegistrations
            (Id,EquipmentId,SourceService,ResourceType,ResourceId,SourceOperationId,State,SourceVersion,RegisteredAtUtc)
            SELECT {Guid.NewGuid()},EquipmentId,SourceService,ResourceType,ResourceId,{Guid.NewGuid()},State,SourceVersion,RegisteredAtUtc
            FROM dbo.EquipmentUseRegistrations WHERE Id={legacy.Id}
            """));
        await db.GetService<IMigrator>().MigrateAsync(Initial);
        Assert.Equal(before, await LegacySnapshot());
        await db.Database.MigrateAsync();
        Assert.Equal(before, await LegacySnapshot());
        Assert.Equal(db.Database.GetMigrations(), await db.Database.GetAppliedMigrationsAsync());
    }

    [SqlServerTheory]
    [InlineData("Coordinating")]
    [InlineData("Cancelled")]
    [InlineData("Completed")]
    [InlineData("Sealed")]
    [InlineData("CancelAck")]
    [InlineData("WorkOrder")]
    [InlineData("AbortedTombstone")]
    public async Task New_migration_down_guard_preserves_every_protocol_kind_including_terminal_history(string kind)
    {
        await using var database = await TestDatabase.CreateAsync("Equipment");
        await using var db = (EquipmentDbContext)Contexts.Create("Equipment", database.ConnectionString);
        await db.Database.MigrateAsync();
        var asset = await SeedAsync(db);
        if (kind is "WorkOrder" or "AbortedTombstone")
        {
            var request = new WorkOrderRegistrationRequest(asset.Id, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                "Maintenance", Now, Now.AddHours(1));
            var registration = EquipmentUseRegistration.RegisterWorkOrder(asset, request, Guid.NewGuid(), 1, "Unit", Now);
            db.Add(registration);
            await db.SaveChangesAsync();
            if (kind == "AbortedTombstone")
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.EquipmentUseRegistrations SET State=N'Aborted',CancelIntentId={Guid.NewGuid()} WHERE Id={registration.Id}");
        }
        else
        {
            var intent = Begin(asset);
            if (kind is "Completed" or "Sealed") Seal(intent);
            if (kind == "Completed")
                asset.Retire(intent, VerifiedRetirementEligibility.Create(intent, true, false, false, false), Now);
            if (kind is "Cancelled" or "CancelAck")
            {
                intent.RequestCancel(Actor, "Cancel", Now);
                Report(intent, "Maintenance", "CancelAck");
                Report(intent, "Incident", "CancelAck");
                if (kind == "Cancelled") intent.ConfirmCancelled(Now);
            }
            db.Add(intent);
            await db.SaveChangesAsync();
        }
        async Task<string> ProtocolSnapshot()
        {
            await using var connection = new SqlConnection(database.ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT
                  (SELECT * FROM dbo.EquipmentRetirementIntents ORDER BY Id FOR JSON PATH,INCLUDE_NULL_VALUES) AS Intents,
                  (SELECT * FROM dbo.RetirementSourceReceipts ORDER BY IntentId,SourceService FOR JSON PATH,INCLUDE_NULL_VALUES) AS Receipts,
                  (SELECT * FROM dbo.EquipmentUseRegistrations ORDER BY Id FOR JSON PATH,INCLUDE_NULL_VALUES) AS Registrations
                FOR JSON PATH,WITHOUT_ARRAY_WRAPPER
                """;
            var json = new System.Text.StringBuilder();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) json.Append(reader.GetString(0));
            return json.ToString();
        }
        var protocolBefore = await ProtocolSnapshot();
        var intents = await db.EquipmentRetirementIntents.AsNoTracking().CountAsync();
        var receipts = await db.RetirementSourceReceipts.AsNoTracking().CountAsync();
        var registrations = await db.EquipmentUseRegistrations.AsNoTracking().CountAsync();
        var e = await Assert.ThrowsAsync<SqlException>(() => db.GetService<IMigrator>().MigrateAsync(Initial));
        Assert.Equal(51021, e.Number);
        Assert.Equal(protocolBefore, await ProtocolSnapshot());
        Assert.Equal(new[] { Initial, Latest }, await db.Database.GetAppliedMigrationsAsync());
        Assert.Equal(intents, await db.EquipmentRetirementIntents.AsNoTracking().CountAsync());
        Assert.Equal(receipts, await db.RetirementSourceReceipts.AsNoTracking().CountAsync());
        Assert.Equal(registrations, await db.EquipmentUseRegistrations.AsNoTracking().CountAsync());
        // Proves a failed Down did not drop any added columns or durable proof values.
        Assert.All(await db.EquipmentUseRegistrations.AsNoTracking().ToListAsync(), x => Assert.NotNull(x.RequestPayloadJson));
        Assert.All(await db.RetirementSourceReceipts.AsNoTracking().ToListAsync(), x =>
            Assert.True(x.ReadyProofId != null || x.CancelAcknowledgementId != null));
    }
}
