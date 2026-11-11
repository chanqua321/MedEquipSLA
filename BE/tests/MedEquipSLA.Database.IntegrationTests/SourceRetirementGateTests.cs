using MedEquipSLA.Application.Coordination;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;
using MaintenanceDb = MedEquipSLA.Maintenance.Infrastructure.Persistence.MaintenanceDbContext;
using IncidentDb = MedEquipSLA.Incident.Infrastructure.Persistence.IncidentDbContext;

namespace MedEquipSLA.Database.IntegrationTests;

public sealed class SourceRetirementGateTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private static ISourceRetirementGate Store(DbContext db) => db switch
    {
        MaintenanceDb maintenance => new Maintenance.Infrastructure.Persistence.SourceRetirementGate(maintenance, TimeProvider.System),
        IncidentDb incident => new Incident.Infrastructure.Persistence.SourceRetirementGate(incident, TimeProvider.System),
        _ => throw new ArgumentException()
    };

    private static Task<EquipmentRetirementGate> LockAsync(DbContext db, Guid equipment) => db switch
    {
        MaintenanceDb maintenance => Maintenance.Infrastructure.Persistence.EquipmentGateAccess.LockAsync(maintenance, equipment, default),
        IncidentDb incident => Incident.Infrastructure.Persistence.EquipmentGateAccess.LockAsync(incident, equipment, default),
        _ => throw new ArgumentException()
    };

    private static void AddPending(DbContext db, Guid equipment, bool legacy = false)
    {
        var hash = new byte[32];
        if (db is MaintenanceDb)
        {
            var row = Maintenance.Infrastructure.Persistence.Entities.IdempotencyRequest.Begin(Guid.NewGuid(), Guid.NewGuid(), hash, Now);
            if (!legacy) row.BindPendingRequest(equipment, "{}", hash);
            db.Add(row);
        }
        else
        {
            var row = Incident.Infrastructure.Persistence.Entities.IdempotencyRequest.Begin(Guid.NewGuid().ToString(), "CreateIncident",
                Guid.NewGuid().ToString(), hash, Now);
            if (!legacy) row.BindPendingRequest(equipment, "{}", hash);
            db.Add(row);
        }
    }

    [Fact]
    public void Cancellation_tombstone_and_epoch_fencing_never_reopen_a_newer_gate()
    {
        var gate = EquipmentRetirementGate.Open(Guid.NewGuid(), Now);
        var first = Guid.NewGuid();
        Assert.True(gate.AcknowledgeCancellation(first, 1, Now));
        Assert.False(gate.Freeze(first, 1, Now));
        Assert.Equal("Open", gate.Phase);
        var second = Guid.NewGuid();
        gate.Freeze(second, 2, Now);
        Assert.False(gate.AcknowledgeCancellation(first, 1, Now));
        Assert.Throws<SourceAdmissionClosedException>(() => gate.AdmitNew(Now));
        Assert.Throws<InvalidOperationException>(() => gate.Seal(true, Now));
        gate.Seal(false, Now);
        var proof = gate.ReadyProofId;
        Assert.False(gate.Seal(false, Now));
        Assert.Equal(proof, gate.ReadyProofId);
        gate.ObserveCompleted(second, 2, Now);
        Assert.Throws<InvalidOperationException>(() => gate.AcknowledgeCancellation(second, 2, Now));
    }

    [SqlServerTheory]
    [InlineData("Maintenance")]
    [InlineData("Incident")]
    public async Task Pending_committed_before_freeze_is_visible_without_equipment_registration(string owner)
    {
        await using var catalog = await TestDatabase.CreateAsync(owner);
        var equipment = Guid.NewGuid();
        await using var writer = Contexts.Create(owner, catalog.ConnectionString);
        await writer.Database.MigrateAsync();
        await using var tx = await writer.Database.BeginTransactionAsync();
        (await LockAsync(writer, equipment)).AdmitNew(Now);
        AddPending(writer, equipment);
        await writer.SaveChangesAsync();
        await using var freezer = Contexts.Create(owner, catalog.ConnectionString);
        // It cannot pass the admission lock while the writer has not committed Pending.
        var freezing = Store(freezer).PrepareAsync(equipment, Guid.NewGuid(), 1, default);
        await tx.CommitAsync();
        var observation = await freezing;
        Assert.Equal("Frozen", observation.Phase);
        Assert.Equal(1, observation.PendingCount);
        Assert.Null(observation.ReadyProofId);
        await using var restarted = Contexts.Create(owner, catalog.ConnectionString);
        var replay = await Store(restarted).PrepareAsync(equipment, observation.IntentId, 1, default);
        Assert.Equal(observation.GateVersion, replay.GateVersion);
        Assert.Equal(1, replay.PendingCount);
    }

    [SqlServerTheory]
    [InlineData("Maintenance")]
    [InlineData("Incident")]
    public async Task Freeze_before_writer_blocks_new_pending_and_duplicate_seal_keeps_one_proof(string owner)
    {
        await using var catalog = await TestDatabase.CreateAsync(owner);
        var equipment = Guid.NewGuid();
        var intent = Guid.NewGuid();
        await using var db = Contexts.Create(owner, catalog.ConnectionString);
        await db.Database.MigrateAsync();
        var sealedState = await Store(db).PrepareAsync(equipment, intent, 1, default);
        Assert.Equal("Sealed", sealedState.Phase);
        Assert.NotNull(sealedState.ReadyProofId);
        await using (var writer = Contexts.Create(owner, catalog.ConnectionString))
        await using (var tx = await writer.Database.BeginTransactionAsync())
        {
            var gate = await LockAsync(writer, equipment);
            Assert.Throws<SourceAdmissionClosedException>(() => gate.AdmitNew(Now));
        }
        await using var restart = Contexts.Create(owner, catalog.ConnectionString);
        var replay = await Store(restart).PrepareAsync(equipment, intent, 1, default);
        Assert.Equal(sealedState.ReadyProofId, replay.ReadyProofId);
        Assert.Equal(sealedState.GateVersion, replay.GateVersion);
        await using var connection = new SqlConnection(catalog.ConnectionString);
        await connection.OpenAsync();
        await using var count = connection.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM dbo.AuditEntries WHERE Action=N'SourceSealed'";
        Assert.Equal(1, Convert.ToInt32(await count.ExecuteScalarAsync()));
    }

    [SqlServerTheory]
    [InlineData("Maintenance")]
    [InlineData("Incident")]
    public async Task Unknown_legacy_pending_blocks_every_equipment_instead_of_guessing_ownership(string owner)
    {
        await using var catalog = await TestDatabase.CreateAsync(owner);
        await using var db = Contexts.Create(owner, catalog.ConnectionString);
        await db.Database.MigrateAsync();
        AddPending(db, Guid.NewGuid(), legacy: true);
        await db.SaveChangesAsync();
        var observation = await Store(db).PrepareAsync(Guid.NewGuid(), Guid.NewGuid(), 1, default);
        Assert.Equal("Frozen", observation.Phase);
        Assert.Equal(1, observation.UnknownCount);
        Assert.Null(observation.ReadyProofId);
    }

    [SqlServerTheory]
    [InlineData("Maintenance")]
    [InlineData("Incident")]
    public async Task Cancel_restart_and_delayed_prepare_keep_tombstone_and_do_not_change_new_epoch(string owner)
    {
        await using var catalog = await TestDatabase.CreateAsync(owner);
        await using var db = Contexts.Create(owner, catalog.ConnectionString);
        await db.Database.MigrateAsync();
        var equipment = Guid.NewGuid();
        var first = Guid.NewGuid();
        var sealedState = await Store(db).PrepareAsync(equipment, first, 1, default);
        var cancelled = await Store(db).ObserveEquipmentDecisionAsync(equipment, first, 1, "Cancelling", default);
        Assert.Equal("Open", cancelled.Phase);
        Assert.NotNull(cancelled.CancelAcknowledgementId);
        var replay = await Store(db).ObserveEquipmentDecisionAsync(equipment, first, 1, "Cancelled", default);
        Assert.Equal(cancelled.CancelAcknowledgementId, replay.CancelAcknowledgementId);
        await using var restart = Contexts.Create(owner, catalog.ConnectionString);
        Assert.Equal("Open", (await Store(restart).PrepareAsync(equipment, first, 1, default)).Phase);
        var second = Guid.NewGuid();
        var next = await Store(restart).PrepareAsync(equipment, second, 2, default);
        Assert.NotEqual(sealedState.ReadyProofId, next.ReadyProofId);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Store(restart).ObserveEquipmentDecisionAsync(equipment, first, 1, "Cancelling", default));
        db.ChangeTracker.Clear();
        var gate = await db.Set<EquipmentRetirementGate>().SingleAsync();
        Assert.Equal(second, gate.CurrentIntentId);
        Assert.Equal("Sealed", gate.Phase);
        Assert.NotEmpty(gate.RowVersion);
    }

    [SqlServerTheory]
    [InlineData("Maintenance")]
    [InlineData("Incident")]
    public async Task Additive_migration_preserves_legacy_and_down_is_guarded_when_protocol_metadata_exists(string owner)
    {
        await using var catalog = await TestDatabase.CreateAsync(owner);
        await using var db = Contexts.Create(owner, catalog.ConnectionString);
        var migrations = db.Database.GetMigrations().ToArray();
        var gateIndex = Array.FindIndex(migrations, x => x.EndsWith("_AddSourceRetirementGate", StringComparison.Ordinal));
        var previous = migrations[gateIndex - 1];
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(previous);
        // Add a pre-existing row with the old schema; new nullable linkage must not be invented.
        await db.Database.ExecuteSqlRawAsync("""
            INSERT dbo.IdempotencyRequests
                (OperationId,ActorScope,OperationName,IdempotencyKey,RequestHash,State,CreatedAtUtc,ExpiresAtUtc)
            VALUES(NEWID(),N'legacy',N'legacy',CONVERT(nvarchar(36),NEWID()),CONVERT(varbinary(32),REPLICATE('x',32)),
                N'Pending',SYSUTCDATETIME(),DATEADD(day,7,SYSUTCDATETIME()))
            """);
        await migrator.MigrateAsync();
        await using (var connection = new SqlConnection(catalog.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM dbo.IdempotencyRequests WHERE ActorScope=N'legacy' AND EquipmentId IS NULL AND RequestPayloadJson IS NULL";
            Assert.Equal(1, Convert.ToInt32(await command.ExecuteScalarAsync()));
        }
        // Safe Down/Up while there is no protocol metadata, preserving the legacy row.
        await migrator.MigrateAsync(previous);
        await migrator.MigrateAsync();
        await Store(db).PrepareAsync(Guid.NewGuid(), Guid.NewGuid(), 1, default);
        var error = await Assert.ThrowsAsync<SqlException>(() => migrator.MigrateAsync(previous));
        Assert.Equal(51031, error.Number);
        // EF commits each migration separately: later metadata-free migrations may roll back first.
        Assert.Equal(migrations.Take(gateIndex + 1), await db.Database.GetAppliedMigrationsAsync());
        Assert.Single(await db.Set<EquipmentRetirementGate>().AsNoTracking().ToArrayAsync());
    }
}
