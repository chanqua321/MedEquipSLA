using System.Text.Json;
using MedEquipSLA.Contracts.Events;
using MedEquipSLA.Equipment.Infrastructure.Persistence;
using MedEquipSLA.Equipment.Infrastructure.Workers;
using MedEquipSLA.Maintenance.Domain;
using MedEquipSLA.Maintenance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using Xunit;
using static MedEquipSLA.Database.IntegrationTests.EquipmentWorkOrderGrpcTests;

namespace MedEquipSLA.Database.IntegrationTests;

public sealed class RegistrationSnapshotTests
{
    private static RegistrationSnapshotConsumer Consumer(IServiceProvider services) => new(new ConfigurationBuilder().Build(),
        services.GetRequiredService<IServiceScopeFactory>(),NullLogger<RegistrationSnapshotConsumer>.Instance);
    private static ServiceProvider Services(string connection) => new ServiceCollection()
        .AddDbContext<EquipmentDbContext>(o=>o.UseSqlServer(connection)).BuildServiceProvider();
    private static RegistrationSnapshotItem Pending(Guid? equipment = null) => new("Incident",Guid.NewGuid(),Guid.NewGuid(),
        equipment,null,"Pending",0,"Unresolved");

    [SqlServerFact]
    public async Task Checkpoint_orders_pages_survives_restart_ignores_duplicates_and_never_releases_absent_or_pending_registration()
    {
        await using var host = await ProtocolHost.CreateAsync();
        var request = host.Request();await host.Client.RegisterWorkOrderAsync(request,host.Credentials);
        await using var target = host.Context();await using var services = Services(target.Database.GetConnectionString()!);
        var pages = RegistrationSnapshotPage.Capture(new[] { Pending(host.AssetId),Pending(null),Pending(host.AssetId) },DateTimeOffset.UtcNow,1);
        using(var consumer = Consumer(services))
        {
            await Assert.ThrowsAnyAsync<InvalidOperationException>(()=>consumer.ProcessAsync(pages[1].Envelope("Incident"),default));
            await consumer.ProcessAsync(pages[0].Envelope("Incident"),default);
            await consumer.ProcessAsync(pages[0].Envelope("Incident"),default);
            await Assert.ThrowsAnyAsync<InvalidOperationException>(()=>consumer.ProcessAsync(pages[2].Envelope("Incident"),default));
        }
        using(var restarted = Consumer(services))
        {
            await restarted.ProcessAsync(pages[1].Envelope("Incident"),default);
            await restarted.ProcessAsync(pages[2].Envelope("Incident"),default);
            await restarted.ProcessAsync(pages[1].Envelope("Incident"),default);
            var newer = RegistrationSnapshotPage.Capture([],DateTimeOffset.UtcNow.AddMinutes(1)).Single();
            await restarted.ProcessAsync(newer.Envelope("Incident"),default);
            await restarted.ProcessAsync(pages[0].Envelope("Incident"),default);
            Assert.Equal(newer.SnapshotId,(await target.ReconciliationCheckpoints.AsNoTracking().SingleAsync()).SnapshotId);
        }
        var checkpoint = await target.ReconciliationCheckpoints.AsNoTracking().SingleAsync();
        Assert.Equal("Completed",checkpoint.State);Assert.Equal("1:0",checkpoint.Cursor);
        Assert.Equal("Pending",(await target.EquipmentUseRegistrations.AsNoTracking().SingleAsync()).State);
        Assert.Equal(4,await target.InboxMessages.CountAsync(x=>x.ConsumerName==RegistrationSnapshotConsumer.Group));
    }

    [SqlServerFact]
    public async Task Page_hash_binding_coverage_and_late_manifest_cannot_overwrite_newer_progress()
    {
        await using var catalog = await TestDatabase.CreateAsync("Equipment");
        await using(var db = Contexts.Create("Equipment",catalog.ConnectionString)) await db.Database.MigrateAsync();
        await using var services = Services(catalog.ConnectionString);using var consumer = Consumer(services);
        var pages = RegistrationSnapshotPage.Capture(new[] { Pending(),Pending() },DateTimeOffset.UtcNow,1);
        var changed = pages[0] with { Items = [Pending()] };
        await Assert.ThrowsAsync<JsonException>(()=>consumer.ProcessAsync(changed.Envelope("Incident"),default));
        await Assert.ThrowsAsync<JsonException>(()=>consumer.ProcessAsync(pages[0].Envelope("Identity"),default));
        await consumer.ProcessAsync(pages[0].Envelope("Incident"),default);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>consumer.ProcessAsync((pages[1] with { TotalItems=3 }).Envelope("Incident"),default));
        await consumer.ProcessAsync(pages[1].Envelope("Incident"),default);
        await using var target = (EquipmentDbContext)Contexts.Create("Equipment",catalog.ConnectionString);
        Assert.Equal("2:2",(await target.ReconciliationCheckpoints.SingleAsync()).Cursor);
        Assert.Equal(2,await target.InboxMessages.CountAsync());
    }

    [SqlServerFact]
    public async Task Inactive_committed_schedule_stays_active_registration_and_empty_inventory_cannot_release_it()
    {
        await using var host = await ProtocolHost.CreateAsync();
        await using var sourceCatalog = await TestDatabase.CreateAsync("Maintenance");
        await using var source = (MaintenanceDbContext)Contexts.Create("Maintenance",sourceCatalog.ConnectionString);
        await source.Database.MigrateAsync();var now = DateTimeOffset.UtcNow;
        var hash = System.Security.Cryptography.SHA256.HashData("payload"u8);
        var op = Maintenance.Infrastructure.Persistence.Entities.IdempotencyRequest.Begin(Guid.NewGuid(),Guid.NewGuid(),hash,now);
        op.BindPendingRequest(host.AssetId,"{}",hash);
        var registered = await host.Client.RegisterScheduleUseAsync(new() { EquipmentId=host.AssetId.ToString(),OperationId=op.OperationId.ToString() },host.RegistrationCredentials("Maintenance"));
        var schedule = MaintenanceSchedule.Create(op.OperationId,host.AssetId,registered.EquipmentCode,registered.EquipmentName,
            Guid.Parse(registered.DepartmentId),registered.DepartmentName,Guid.Parse(registered.RegistrationId),"Maintenance",now,7,60,30,false,Guid.NewGuid(),now);
        op.Commit("{}");source.AddRange(op,schedule);await source.SaveChangesAsync();
        var pages = await Maintenance.Infrastructure.Workers.RegistrationSnapshotExporter.CaptureAsync(source,now,default);
        Assert.Single(pages);Assert.Empty(await Maintenance.Infrastructure.Workers.RegistrationSnapshotExporter.CaptureAsync(source,now,default));
        await using var target = host.Context();await using var services=Services(target.Database.GetConnectionString()!);
        using var consumer=Consumer(services);await consumer.ProcessAsync(pages[0].Envelope("Maintenance"),default);
        Assert.Equal("Active",(await target.EquipmentUseRegistrations.AsNoTracking().SingleAsync()).State);
        await consumer.ProcessAsync(RegistrationSnapshotPage.Capture([],now.AddMinutes(1))[0].Envelope("Maintenance"),default);
        Assert.Equal("Active",(await target.EquipmentUseRegistrations.AsNoTracking().SingleAsync()).State);
        Assert.Equal("Operational",(await target.EquipmentAssets.SingleAsync()).OperationalStatus);
        Assert.Equal(1,await source.AuditEntries.CountAsync(x=>x.Action=="RegistrationSnapshotCaptured"));
    }

    [RedisSqlFact]
    public async Task Real_source_outbox_and_redis_snapshot_repairs_missing_workorder_event_without_clearing_broken_and_old_snapshot_cannot_reopen_terminal_registration()
    {
        await using var host=await ProtocolHost.CreateAsync();
        await using var sourceCatalog=await TestDatabase.CreateAsync("Maintenance");
        await using var source=(MaintenanceDbContext)Contexts.Create("Maintenance",sourceCatalog.ConnectionString);
        await source.Database.MigrateAsync();var now=DateTimeOffset.UtcNow;
        var schedule=MaintenanceSchedule.Create(Guid.NewGuid(),host.AssetId,"EQ","Device",Guid.NewGuid(),"Unit",Guid.NewGuid(),
            "Maintenance",now.AddMinutes(-2),7,60,30,true,Guid.NewGuid(),now);
        var occurrence=schedule.PlanNext(now);source.AddRange(schedule,occurrence);await source.SaveChangesAsync();
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
            ["Grpc:MaintenanceWorkOrderKey"]=host.Credentials.GetValue("x-maintenance-workorder-key") }).Build();
        Assert.Equal(1,await new WorkOrderGeneration(source,host.Client,config,TimeProvider.System).RunBatchAsync(default));
        var order=await source.WorkOrders.SingleAsync();
        await using var target=host.Context();
        await target.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.EquipmentAssets SET OperationalStatus=N'Broken' WHERE Id={host.AssetId}");
        var pages=await Maintenance.Infrastructure.Workers.RegistrationSnapshotExporter.CaptureAsync(source,now,default);
        await using var redis=await ConnectionMultiplexer.ConnectAsync(Environment.GetEnvironmentVariable("REDIS_TEST_CONNECTION")!);
        RedisKey stream=RegistrationSnapshotPage.Stream;
        Assert.False(await redis.GetDatabase().KeyExistsAsync(stream));
        Assert.False(await redis.GetDatabase().KeyExistsAsync(WorkOrderLifecycleEvent.Stream));
        try
        {
            await using var services=Services(target.Database.GetConnectionString()!);using var consumer=Consumer(services);
            var dispatcher=new Maintenance.Infrastructure.Workers.ScheduleOutboxDispatcher(config,services.GetRequiredService<IServiceScopeFactory>(),TimeProvider.System,
                NullLogger<Maintenance.Infrastructure.Workers.ScheduleOutboxDispatcher>.Instance);
            await dispatcher.PublishBatchAsync(source,new Maintenance.Infrastructure.Messaging.RedisEventPublisher(redis),default);
            var entry=Assert.Single(await redis.GetDatabase().StreamRangeAsync(stream));
            var message=JsonSerializer.Deserialize<IntegrationEvent>(entry.Values.Single(x=>x.Name=="event").Value.ToString())!;
            await target.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.AuditEntries ADD CONSTRAINT CK_Test_SnapshotCommit CHECK(Action<>N'RegistrationSnapshotPageApplied')");
            await Assert.ThrowsAsync<DbUpdateException>(()=>consumer.ProcessAsync(message,default));
            Assert.Empty(await target.ReconciliationCheckpoints.AsNoTracking().ToArrayAsync());
            Assert.Equal("Active",(await target.EquipmentUseRegistrations.AsNoTracking().SingleAsync()).State);
            await target.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.AuditEntries DROP CONSTRAINT CK_Test_SnapshotCommit");
            await consumer.ProcessAsync(message,default);await consumer.ProcessAsync(message,default);
            Assert.Equal(1,await target.AuditEntries.CountAsync(x=>x.Action=="WorkOrderProjectionApplied"));
            Assert.Equal("Active",(await target.EquipmentUseRegistrations.AsNoTracking().SingleAsync()).State);
            Assert.Equal("Broken",(await target.EquipmentAssets.AsNoTracking().SingleAsync()).OperationalStatus);
            Assert.False((await target.EquipmentStateProjections.AsNoTracking().SingleAsync()).IsBlocking);
            order.Cancel(Guid.NewGuid(),"Not started",now.AddMinutes(1));await source.SaveChangesAsync();
            // A later complete source inventory supersedes the older snapshot, even if live events were lost.
            await source.OutboxMessages.Where(x=>x.EventType==RegistrationSnapshotPage.EventType).ExecuteUpdateAsync(u=>u.SetProperty(x=>x.Status,"Published"));
            var terminal=await Maintenance.Infrastructure.Workers.RegistrationSnapshotExporter.CaptureAsync(source,now.AddMinutes(6),default);
            foreach(var page in terminal) await consumer.ProcessAsync(page.Envelope("Maintenance"),default);
            await consumer.ProcessAsync(pages[0].Envelope("Maintenance"),default);
            Assert.Equal("Released",(await target.EquipmentUseRegistrations.AsNoTracking().SingleAsync()).State);
            Assert.Equal("Broken",(await target.EquipmentAssets.AsNoTracking().SingleAsync()).OperationalStatus);
        }
        finally
        {
            await redis.GetDatabase().KeyDeleteAsync(stream);
            // Publisher also emitted the fixture's original WorkOrderCreated event; this dedicated stream is owned here.
            await redis.GetDatabase().KeyDeleteAsync(WorkOrderLifecycleEvent.Stream);
        }
    }

    [SqlServerFact]
    public async Task Source_incident_snapshot_restores_positive_closed_proof_and_newer_live_version_wins_over_old_snapshot()
    {
        await using var host=await ProtocolHost.CreateAsync();
        await using var catalog=await TestDatabase.CreateAsync("Incident");
        await using var source=(Incident.Infrastructure.Persistence.IncidentDbContext)Contexts.Create("Incident",catalog.ConnectionString);
        await source.Database.MigrateAsync();var now=DateTimeOffset.UtcNow;var actor=Guid.NewGuid();
        var hash=System.Security.Cryptography.SHA256.HashData("incident payload"u8);
        var op=Incident.Infrastructure.Persistence.Entities.IdempotencyRequest.Begin(actor.ToString(),"CreateIncident",Guid.NewGuid().ToString(),hash,now);
        op.BindPendingRequest(host.AssetId,"{}",hash);
        var registration=await host.Client.RegisterIncidentUseAsync(new() { EquipmentId=host.AssetId.ToString(),OperationId=op.OperationId.ToString() },host.RegistrationCredentials("Incident"));
        var policy=Incident.Domain.SlaPolicy.Create("Medium",actor,now);
        var version=Incident.Domain.SlaPolicyVersion.Start(policy.Id,1,60,120,actor,now);
        var incident=Incident.Domain.IncidentRecord.Open(op.OperationId,host.AssetId,registration.EquipmentCode,registration.EquipmentName,
            Guid.Parse(registration.DepartmentId),registration.DepartmentName,Guid.Parse(registration.RegistrationId),actor,"Broken","Stopped","Medium",null,version,now);
        op.Commit(incident.Id);source.AddRange(op,policy,version,incident);await source.SaveChangesAsync();
        var opened=await Incident.Infrastructure.Workers.RegistrationSnapshotExporter.CaptureAsync(source,now,default);
        await using var target=host.Context();await using var services=Services(target.Database.GetConnectionString()!);
        using var consumer=Consumer(services);
        await Task.WhenAll(consumer.ProcessAsync(opened.Single().Envelope("Incident"),default),consumer.ProcessAsync(opened.Single().Envelope("Incident"),default));
        Assert.Equal("Broken",(await target.EquipmentAssets.AsNoTracking().SingleAsync()).OperationalStatus);
        incident.Assign(actor,actor,now);incident.StartRepair(actor,now.AddMinutes(1));incident.SubmitRepair(actor,now.AddMinutes(2));
        var attempt=Incident.Domain.ResolutionAttempt.Submit(incident.Id,1,actor,"Repaired","Running",now.AddMinutes(2),[1,2,3],"image/png");
        attempt.Accept(actor,now.AddMinutes(3));incident.Close(attempt,actor,now.AddMinutes(3));source.Add(attempt);await source.SaveChangesAsync();
        await source.OutboxMessages.ExecuteUpdateAsync(u=>u.SetProperty(x=>x.Status,"Published"));
        var closed=await Incident.Infrastructure.Workers.RegistrationSnapshotExporter.CaptureAsync(source,now.AddMinutes(6),default);
        await consumer.ProcessAsync(closed.Single().Envelope("Incident"),default);
        await consumer.ProcessAsync(opened.Single().Envelope("Incident"),default);
        Assert.Equal("Released",(await target.EquipmentUseRegistrations.AsNoTracking().SingleAsync()).State);
        Assert.Equal("Operational",(await target.EquipmentAssets.AsNoTracking().SingleAsync()).OperationalStatus);
        Assert.Equal("Closed",(await target.EquipmentStateProjections.AsNoTracking().SingleAsync()).SourceStatus);
    }

    [SqlServerFact]
    public async Task Concurrent_source_capture_has_one_manifest_and_audit_and_sql_failure_does_not_leave_partial_snapshot()
    {
        await using var catalog=await TestDatabase.CreateAsync("Incident");
        await using(var db=Contexts.Create("Incident",catalog.ConnectionString)) await db.Database.MigrateAsync();
        async Task<int> Capture()
        {
            await using var db=(Incident.Infrastructure.Persistence.IncidentDbContext)Contexts.Create("Incident",catalog.ConnectionString);
            return (await Incident.Infrastructure.Workers.RegistrationSnapshotExporter.CaptureAsync(db,DateTimeOffset.UtcNow,default)).Length;
        }
        Assert.Equal(1,(await Task.WhenAll(Capture(),Capture())).Sum());
        await using var source=(Incident.Infrastructure.Persistence.IncidentDbContext)Contexts.Create("Incident",catalog.ConnectionString);
        Assert.Equal(1,await source.OutboxMessages.CountAsync());Assert.Equal(1,await source.AuditEntries.CountAsync());
        await source.OutboxMessages.ExecuteUpdateAsync(u=>u.SetProperty(x=>x.Status,"Published"));
        await source.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.AuditEntries WITH NOCHECK ADD CONSTRAINT CK_Test_SnapshotAudit CHECK(Action<>N'RegistrationSnapshotCaptured')");
        await Assert.ThrowsAsync<DbUpdateException>(()=>Incident.Infrastructure.Workers.RegistrationSnapshotExporter.CaptureAsync(source,DateTimeOffset.UtcNow.AddMinutes(6),default));
        source.ChangeTracker.Clear();
        Assert.Equal(1,await source.OutboxMessages.CountAsync());Assert.Equal(1,await source.AuditEntries.CountAsync());
    }

    [RedisSqlFact]
    public async Task Hosted_snapshot_consumer_reads_real_source_dispatcher_stream_and_acknowledges_only_after_checkpoint_commit()
    {
        await using var sourceCatalog=await TestDatabase.CreateAsync("Incident");
        await using var targetCatalog=await TestDatabase.CreateAsync("Equipment");
        await using var source=(Incident.Infrastructure.Persistence.IncidentDbContext)Contexts.Create("Incident",sourceCatalog.ConnectionString);
        await source.Database.MigrateAsync();
        await using(var db=Contexts.Create("Equipment",targetCatalog.ConnectionString)) await db.Database.MigrateAsync();
        var pages=await Incident.Infrastructure.Workers.RegistrationSnapshotExporter.CaptureAsync(source,DateTimeOffset.UtcNow,default);
        await using var redis=await ConnectionMultiplexer.ConnectAsync(Environment.GetEnvironmentVariable("REDIS_TEST_CONNECTION")!);
        Assert.False(await redis.GetDatabase().KeyExistsAsync(RegistrationSnapshotPage.Stream));
        await using var services=new ServiceCollection().AddSingleton<IConnectionMultiplexer>(redis)
            .AddDbContext<EquipmentDbContext>(o=>o.UseSqlServer(targetCatalog.ConnectionString)).BuildServiceProvider();
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Workers:Enabled"]="true" }).Build();
        using var consumer=new RegistrationSnapshotConsumer(config,services.GetRequiredService<IServiceScopeFactory>(),NullLogger<RegistrationSnapshotConsumer>.Instance);
        try
        {
            using var dispatcher=new Incident.Infrastructure.Workers.SlaOutboxDispatcher(config,services.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<Incident.Infrastructure.Workers.SlaOutboxDispatcher>.Instance);
            var publisher=new Incident.Infrastructure.Messaging.RedisEventPublisher(redis);
            await dispatcher.PublishBatchAsync(source,publisher,default);
            await consumer.StartAsync(default);
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));
            while(true)
            {
                await using var db=(EquipmentDbContext)Contexts.Create("Equipment",targetCatalog.ConnectionString);
                if(await db.ReconciliationCheckpoints.AnyAsync(x=>x.State=="Completed",timeout.Token) &&
                    (await redis.GetDatabase().StreamPendingAsync(RegistrationSnapshotPage.Stream,RegistrationSnapshotConsumer.Group)).PendingMessageCount==0) break;
                await Task.Delay(100,timeout.Token);
            }
            await publisher.PublishAsync(RegistrationSnapshotPage.Stream,pages.Single().Envelope("Incident"),default);
            var last=(await redis.GetDatabase().StreamInfoAsync(RegistrationSnapshotPage.Stream)).LastEntry.Id;
            while(true)
            {
                var group=(await redis.GetDatabase().StreamGroupInfoAsync(RegistrationSnapshotPage.Stream)).Single();
                if(group.LastDeliveredId==last && group.PendingMessageCount==0) break;
                await Task.Delay(100,timeout.Token);
            }
            // A duplicate remains safe even when a restarted handler receives the same page.
            using(var restarted=Consumer(services)) await restarted.ProcessAsync(pages.Single().Envelope("Incident"),default);
            await using var target=(EquipmentDbContext)Contexts.Create("Equipment",targetCatalog.ConnectionString);
            Assert.Equal(1,await target.InboxMessages.CountAsync());
            Assert.Equal(1,await target.AuditEntries.CountAsync(x=>x.Action=="RegistrationSnapshotPageApplied"));
        }
        finally
        {
            await consumer.StopAsync(default);
            await redis.GetDatabase().KeyDeleteAsync(RegistrationSnapshotPage.Stream);
        }
    }
}
