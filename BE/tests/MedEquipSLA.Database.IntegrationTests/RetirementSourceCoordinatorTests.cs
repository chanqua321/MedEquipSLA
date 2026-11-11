using System.Net.Http.Headers;
using System.Net.Http.Json;
using MedEquipSLA.Application.Coordination;
using MedEquipSLA.Contracts.Grpc.Equipment.V1;
using MedEquipSLA.Equipment.Application.Features.Retirement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;
using static MedEquipSLA.Database.IntegrationTests.EquipmentWorkOrderGrpcTests;

namespace MedEquipSLA.Database.IntegrationTests;

public sealed class RetirementSourceCoordinatorTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private static Func<RetirementIntentReply, Task<bool>> Coordinator(string owner, DbContext db, ProtocolHost host,
        TimeProvider clock, bool wrongKey = false)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Grpc:RetirementSourceKey"] = wrongKey ? "invalid-test-key" : host.RetirementCredentials(owner).GetValue("x-retirement-source-key")
        }).Build();
        if (db is Maintenance.Infrastructure.Persistence.MaintenanceDbContext maintenance)
        {
            var coordinator = new Maintenance.Infrastructure.Workers.RetirementSourceCoordinator(maintenance,
                new Maintenance.Infrastructure.Persistence.SourceRetirementGate(maintenance, clock), host.Client, config, clock);
            return reply => coordinator.CoordinateAsync(reply, default);
        }
        var incident = (Incident.Infrastructure.Persistence.IncidentDbContext)db;
        var other = new Incident.Infrastructure.Workers.RetirementSourceCoordinator(incident,
            new Incident.Infrastructure.Persistence.SourceRetirementGate(incident, clock), host.Client, config, clock);
        return reply => other.CoordinateAsync(reply, default);
    }
    private static async Task<RetirementView> BeginAsync(ProtocolHost host)
    {
        await using var db = host.Context();
        var value = await new Equipment.Infrastructure.Persistence.RetirementStore(db).BeginAsync(host.AssetId,
            EquipmentRetirementDomainTests.Actor, Guid.NewGuid(), "Reviewed", "source-worker-test", default);
        Assert.Equal(RetirementOutcome.Saved, value.Outcome);
        return value.Value!;
    }
    private static RetirementIntentReply Discover(RetirementView view) => new()
    {
        EquipmentId = view.EquipmentId.ToString(), IntentId = view.Id.ToString(),
        RetirementEpoch = view.Epoch, State = view.State, ContinueCount = view.ContinueCount
    };

    [SqlServerTheory]
    [InlineData("Maintenance")]
    [InlineData("Incident")]
    public async Task Coordinator_polls_equipment_and_recovers_cancellation_after_source_restart(string owner)
    {
        await using var host = await ProtocolHost.CreateAsync();
        await using var catalog = await TestDatabase.CreateAsync(owner);
        var intent = await BeginAsync(host);
        var clock = new Clock();
        await using (var source = Contexts.Create(owner, catalog.ConnectionString))
        {
            await source.Database.MigrateAsync();
            Assert.True(await Coordinator(owner, source, host, clock)(Discover(intent)));
            Assert.Equal("Sealed", (await source.Set<EquipmentRetirementGate>().SingleAsync()).Phase);
        }
        await using (var equipment = host.Context())
        {
            var view = (await new Equipment.Infrastructure.Persistence.RetirementStore(equipment).FindAsync(host.AssetId, intent.Id, default))!;
            Assert.Single(view.Receipts);
            Assert.Equal("Sealed", view.Receipts[0].State);
            Assert.Equal(RetirementOutcome.Saved, (await new Equipment.Infrastructure.Persistence.RetirementStore(equipment)
                .AdminAsync(host.AssetId, intent.Id, EquipmentRetirementDomainTests.Actor, Guid.NewGuid(),
                    view.RowVersion, "Cancel", "Keep device", "cancel-test", default)).Outcome);
        }
        clock.Now = clock.Now.AddMinutes(10);
        // Stale discovery still must query Equipment and see its durable Cancelling decision before unfreeze.
        await using var restarted = Contexts.Create(owner, catalog.ConnectionString);
        Assert.True(await Coordinator(owner, restarted, host, clock)(Discover(intent)));
        var gate = await restarted.Set<EquipmentRetirementGate>().SingleAsync();
        Assert.Equal("Open", gate.Phase);
        Assert.NotNull(gate.CancelAcknowledgementId);
        await using var check = host.Context();
        var receipt = await check.RetirementSourceReceipts.SingleAsync();
        Assert.Equal(owner, receipt.SourceService);
        Assert.Equal("CancelAck", receipt.SourceState);
        Assert.Equal("Cancelling", (await check.EquipmentRetirementIntents.SingleAsync()).State);
        Assert.Equal(4, await restarted.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS Value FROM dbo.AuditEntries WHERE Action IN (N'RetirementCoordinationAttempt',N'RetirementCoordinationResult')").SingleAsync());
    }

    [SqlServerTheory]
    [InlineData("Maintenance")]
    [InlineData("Incident")]
    public async Task Invalid_source_credentials_record_unknown_without_inventing_a_sealed_gate(string owner)
    {
        await using var host = await ProtocolHost.CreateAsync();
        await using var catalog = await TestDatabase.CreateAsync(owner);
        var intent = await BeginAsync(host);
        await using var source = Contexts.Create(owner, catalog.ConnectionString);
        await source.Database.MigrateAsync();
        Assert.False(await Coordinator(owner, source, host, new Clock(), wrongKey: true)(Discover(intent)));
        Assert.Empty(await source.Set<EquipmentRetirementGate>().ToArrayAsync());
        Assert.Equal(1, await source.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS Value FROM dbo.AuditEntries WHERE Action=N'RetirementCoordinationUnknown'").SingleAsync());
        await using var equipment = host.Context();
        Assert.Empty(await equipment.RetirementSourceReceipts.ToArrayAsync());
        Assert.False((await equipment.EquipmentAssets.SingleAsync()).IsRetired);
    }

    [SqlServerTheory]
    [InlineData("Maintenance")]
    [InlineData("Incident")]
    public async Task Eight_action_attempts_are_durable_and_admin_continue_opens_a_new_retry_series(string owner)
    {
        await using var host = await ProtocolHost.CreateAsync();
        await using var catalog = await TestDatabase.CreateAsync(owner);
        var intent = await BeginAsync(host);
        var clock = new Clock();
        await using (var source = Contexts.Create(owner, catalog.ConnectionString))
        {
            await source.Database.MigrateAsync();
            var hash = new byte[32];
            if (source is Maintenance.Infrastructure.Persistence.MaintenanceDbContext)
            {
                var pending = Maintenance.Infrastructure.Persistence.Entities.IdempotencyRequest.Begin(Guid.NewGuid(), Guid.NewGuid(), hash, clock.Now);
                pending.BindPendingRequest(host.AssetId, "{}", hash);
                source.Add(pending);
            }
            else
            {
                var pending = Incident.Infrastructure.Persistence.Entities.IdempotencyRequest.Begin(Guid.NewGuid().ToString(), "CreateIncident", Guid.NewGuid().ToString(), hash, clock.Now);
                pending.BindPendingRequest(host.AssetId, "{}", hash);
                source.Add(pending);
            }
            await source.SaveChangesAsync();
        }
        for (var attempt = 0; attempt < 8; attempt++)
        {
            // A new DbContext/coordinator each time models restart; the budget is not in memory.
            await using var source = Contexts.Create(owner, catalog.ConnectionString);
            Assert.True(await Coordinator(owner, source, host, clock)(Discover(intent)));
            clock.Now = clock.Now.AddMinutes(10);
        }
        await using (var source = Contexts.Create(owner, catalog.ConnectionString))
        {
            Assert.False(await Coordinator(owner, source, host, clock)(Discover(intent)));
            Assert.Equal(8, await source.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) AS Value FROM dbo.AuditEntries WHERE Action=N'RetirementCoordinationAttempt'").SingleAsync());
            Assert.Equal("Frozen", (await source.Set<EquipmentRetirementGate>().SingleAsync()).Phase);
        }
        await using (var db = host.Context())
        {
            var store = new Equipment.Infrastructure.Persistence.RetirementStore(db);
            var current = (await store.FindAsync(host.AssetId, intent.Id, default))!;
            var continued = await store.AdminAsync(host.AssetId, intent.Id, EquipmentRetirementDomainTests.Actor,
                Guid.NewGuid(), current.RowVersion, "Continue", "Retry coordination", "admin-retry", default);
            Assert.Equal(1, continued.Value!.ContinueCount);
        }
        clock.Now = clock.Now.AddMinutes(10);
        await using var restarted = Contexts.Create(owner, catalog.ConnectionString);
        // Even stale discovery obtains the new ContinueCount through authenticated Get.
        Assert.True(await Coordinator(owner, restarted, host, clock)(Discover(intent)));
        Assert.Equal(9, await restarted.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS Value FROM dbo.AuditEntries WHERE Action=N'RetirementCoordinationAttempt'").SingleAsync());
    }
}
