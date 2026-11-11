using System.Text.Json;
using Grpc.Core;
using MedEquipSLA.Contracts.Events;
using MedEquipSLA.Contracts.Grpc.Equipment.V1;
using MedEquipSLA.Maintenance.Domain;
using MedEquipSLA.Maintenance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using Xunit;
using static MedEquipSLA.Database.IntegrationTests.EquipmentWorkOrderGrpcTests;
using WireRequest = MedEquipSLA.Contracts.Grpc.Equipment.V1.WorkOrderRegistrationRequest;

namespace MedEquipSLA.Database.IntegrationTests;

public sealed class WorkOrderGenerationTests
{
    private sealed class Clock : TimeProvider
    {
        internal DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    // Fault injection happens only after the real Equipment gRPC transaction has committed.
    private sealed class AfterEquipment(EquipmentLookup.EquipmentLookupClient inner, Func<Task> after) : EquipmentLookup.EquipmentLookupClient
    {
        public override AsyncUnaryCall<WorkOrderRegistrationReply> RegisterWorkOrderAsync(WireRequest request,
            Metadata headers = null!, DateTime? deadline = null, CancellationToken cancellationToken = default)
        {
            var call = inner.RegisterWorkOrderAsync(request, headers, deadline, cancellationToken);
            async Task<WorkOrderRegistrationReply> Response()
            { var reply = await call; await after(); return reply; }
            return new(Response(), call.ResponseHeadersAsync, call.GetStatus, call.GetTrailers, call.Dispose);
        }
    }
    private static MaintenanceDbContext Db(TestDatabase catalog) =>
        (MaintenanceDbContext)Contexts.Create("Maintenance", catalog.ConnectionString);
    private static IConfiguration Config(ProtocolHost host) => new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string?> { ["Grpc:MaintenanceWorkOrderKey"] = host.Credentials.GetValue("x-maintenance-workorder-key") }).Build();
    private static WorkOrderGeneration Generator(MaintenanceDbContext db, ProtocolHost host, Clock clock,
        EquipmentLookup.EquipmentLookupClient? client = null) => new(db, client ?? host.Client, Config(host), clock);

    private static async Task<ScheduleOccurrence> SeedAsync(TestDatabase catalog, ProtocolHost host, Clock clock, bool future = false, string workType = "Maintenance")
    {
        await using var db = Db(catalog);
        await db.Database.MigrateAsync();
        var anchor = future ? clock.Now.AddMinutes(30) : clock.Now.AddMinutes(-10);
        var schedule = MaintenanceSchedule.Create(Guid.NewGuid(), host.AssetId, "EQ", "Device", Guid.NewGuid(), "Unit",
            Guid.NewGuid(), workType, anchor, 7, 60, 30, true, Guid.NewGuid(), clock.Now.AddDays(-1));
        var occurrence = schedule.PlanNext(clock.Now);
        db.AddRange(schedule, occurrence);
        await db.SaveChangesAsync();
        return occurrence;
    }

    [SqlServerTheory]
    [InlineData("Operational")]
    [InlineData("Broken")]
    public async Task Two_workers_create_exactly_one_registered_workorder_outbox_and_created_event_is_idempotent(string initialStatus)
    {
        await using var host = await ProtocolHost.CreateAsync();
        if (initialStatus == "Broken")
        {
            await using var targetSeed = host.Context();
            await targetSeed.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.EquipmentAssets SET OperationalStatus={initialStatus} WHERE Id={host.AssetId}");
        }
        await using var catalog = await TestDatabase.CreateAsync("Maintenance");
        var clock = new Clock();
        var occurrence = await SeedAsync(catalog, host, clock);
        async Task Run()
        { await using var db = Db(catalog); await Generator(db, host, clock).RunBatchAsync(default); }
        await Task.WhenAll(Run(), Run());
        await using var source = Db(catalog);
        var order = await source.WorkOrders.SingleAsync();
        var operation = await source.IdempotencyRequests.SingleAsync();
        Assert.Equal("Committed", operation.State);
        Assert.Equal(order.Id, operation.ResourceId);
        Assert.Equal(occurrence.ScheduledOccurrenceAtUtc, order.ScheduledForUtc);
        Assert.Equal(occurrence.DueAtUtc, order.DueAtUtc);
        Assert.Equal("WorkOrderCreated", (await source.ScheduleOccurrences.SingleAsync()).State);
        Assert.Equal(1, await source.AuditEntries.CountAsync(x => x.Action == "WorkOrderCreated"));
        var outbox = await source.OutboxMessages.SingleAsync();
        Assert.Equal("Pending", outbox.Status);
        await using var target = host.Context();
        Assert.Equal("Pending", (await target.EquipmentUseRegistrations.SingleAsync()).State);
        var connection = target.Database.GetConnectionString();
        await using var services = new ServiceCollection()
            .AddDbContext<Equipment.Infrastructure.Persistence.EquipmentDbContext>(o => o.UseSqlServer(connection))
            .BuildServiceProvider();
        using var consumer = new Equipment.Infrastructure.Workers.WorkOrderLifecycleConsumer(
            new ConfigurationBuilder().Build(), services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<Equipment.Infrastructure.Workers.WorkOrderLifecycleConsumer>.Instance);
        var message = new IntegrationEvent(outbox.EventId, outbox.EventType, outbox.SchemaVersion, "Maintenance", outbox.AggregateId,
            outbox.AggregateVersion, outbox.OccurredAtUtc, outbox.CorrelationId, outbox.PayloadJson);
        var entry = new StreamEntry("1-0", [new NameValueEntry("event", JsonSerializer.Serialize(message))]);
        await consumer.ProcessAsync(entry, default);
        await consumer.ProcessAsync(entry, default);
        target.ChangeTracker.Clear();
        Assert.Equal("Active", (await target.EquipmentUseRegistrations.SingleAsync()).State);
        Assert.Equal(initialStatus, (await target.EquipmentAssets.SingleAsync()).OperationalStatus);
        Assert.False((await target.EquipmentStateProjections.SingleAsync()).IsBlocking);
        Assert.Equal(1, await target.InboxMessages.CountAsync());
        Assert.Equal(1, await target.AuditEntries.CountAsync(x => x.Action == "WorkOrderProjectionApplied"));
    }

    [SqlServerFact]
    public async Task Commit_failure_after_remote_registration_keeps_pending_and_restart_retries_same_identity()
    {
        await using var host = await ProtocolHost.CreateAsync();
        await using var catalog = await TestDatabase.CreateAsync("Maintenance");
        var clock = new Clock();
        await SeedAsync(catalog, host, clock);
        await using (var db = Db(catalog))
        {
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.OutboxMessages ADD CONSTRAINT CK_Test_GenerationFailure CHECK(EventType<>N'WorkOrderCreated')");
            Assert.Equal(0, await Generator(db, host, clock).RunBatchAsync(default));
            Assert.Empty(await db.WorkOrders.AsNoTracking().ToArrayAsync());
            Assert.Empty(await db.OutboxMessages.AsNoTracking().ToArrayAsync());
            var operation = await db.IdempotencyRequests.AsNoTracking().SingleAsync();
            Assert.Equal("Pending", operation.State);
            Assert.Equal("DbUpdateException", operation.LastError);
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.OutboxMessages DROP CONSTRAINT CK_Test_GenerationFailure");
        }
        Guid registration;
        await using (var equipment = host.Context()) registration = (await equipment.EquipmentUseRegistrations.SingleAsync()).Id;
        clock.Now = clock.Now.AddMinutes(2);
        await using var restarted = Db(catalog);
        Assert.Equal(1, await Generator(restarted, host, clock).RunBatchAsync(default));
        Assert.Equal(registration, (await restarted.WorkOrders.SingleAsync()).EquipmentUseRegistrationId);
        var saved = await restarted.IdempotencyRequests.SingleAsync();
        Assert.Equal(2, saved.TotalAttemptCount);
        Assert.Equal(2, saved.FencingToken);
        await using var check = host.Context();
        Assert.Equal(1, await check.EquipmentUseRegistrations.CountAsync());
    }

    [SqlServerFact]
    public async Task Lost_reply_after_equipment_commit_can_retry_after_restart_without_second_registration()
    {
        await using var host = await ProtocolHost.CreateAsync();
        await using var catalog = await TestDatabase.CreateAsync("Maintenance");
        var clock = new Clock();
        await SeedAsync(catalog, host, clock);
        var transport = new AfterEquipment(host.Client, () => throw new RpcException(new(StatusCode.DeadlineExceeded, "Injected lost response")));
        await using (var db = Db(catalog))
        {
            Assert.Equal(0, await Generator(db, host, clock, transport).RunBatchAsync(default));
            Assert.Equal("DeadlineExceeded", (await db.IdempotencyRequests.AsNoTracking().SingleAsync()).LastError);
            Assert.Empty(await db.WorkOrders.ToArrayAsync());
        }
        clock.Now = clock.Now.AddMinutes(2);
        await using var restart = Db(catalog);
        Assert.Equal(1, await Generator(restart, host, clock).RunBatchAsync(default));
        await using var equipment = host.Context();
        Assert.Equal(1, await equipment.EquipmentUseRegistrations.CountAsync());
    }

    [SqlServerFact]
    public async Task Inactive_during_remote_call_keeps_pending_then_admin_cancel_obtains_abort_tombstone()
    {
        await using var host = await ProtocolHost.CreateAsync();
        await using var catalog = await TestDatabase.CreateAsync("Maintenance");
        var clock = new Clock();
        await SeedAsync(catalog, host, clock);
        var transport = new AfterEquipment(host.Client, async () =>
        {
            await using var db = Db(catalog);
            await using var tx = await db.Database.BeginTransactionAsync();
            await EquipmentGateAccess.LockAsync(db, host.AssetId, default, clock.Now);
            var schedule = await db.MaintenanceSchedules.SingleAsync();
            schedule.Revise(schedule.AnchorAtUtc, schedule.NextOccurrenceAtUtc, schedule.IntervalDays,
                schedule.ReminderLeadMinutes, schedule.CompletionWindowMinutes, false, Guid.NewGuid(), clock.Now);
            await db.SaveChangesAsync(); await tx.CommitAsync();
        });
        await using (var db = Db(catalog))
        {
            Assert.Equal(0, await Generator(db, host, clock, transport).RunBatchAsync(default));
            Assert.Empty(await db.WorkOrders.ToArrayAsync());
            var op = await db.IdempotencyRequests.SingleAsync();
            Assert.Equal("Pending", op.State);
            op.RequestCancellation(Guid.NewGuid(), "Cancel unused generation", clock.Now);
            await db.SaveChangesAsync();
        }
        clock.Now = clock.Now.AddMinutes(2);
        await using var restarted = Db(catalog);
        Assert.Equal(1, await Generator(restarted, host, clock).RunBatchAsync(default));
        Assert.Equal("Aborted", (await restarted.IdempotencyRequests.SingleAsync()).State);
        Assert.Equal("Cancelled", (await restarted.ScheduleOccurrences.SingleAsync()).State);
        Assert.Empty(await restarted.WorkOrders.ToArrayAsync());
        await using var equipment = host.Context();
        Assert.Equal("Aborted", (await equipment.EquipmentUseRegistrations.SingleAsync()).State);
    }

    [SqlServerFact]
    public async Task Future_occurrence_is_not_generated_early()
    {
        await using var host = await ProtocolHost.CreateAsync();
        await using var catalog = await TestDatabase.CreateAsync("Maintenance");
        var clock = new Clock();
        await SeedAsync(catalog, host, clock, future: true);
        await using var db = Db(catalog);
        Assert.Equal(0, await Generator(db, host, clock).RunBatchAsync(default));
        Assert.Empty(await db.IdempotencyRequests.ToArrayAsync());
        Assert.Empty(await db.WorkOrders.ToArrayAsync());
    }

    [SqlServerFact]
    public async Task Calibration_failed_cannot_close_or_cancel_and_passed_resubmission_keeps_attempts_and_photo()
    {
        await using var host = await ProtocolHost.CreateAsync();
        await using var catalog = await TestDatabase.CreateAsync("Maintenance");
        var clock = new Clock();
        await SeedAsync(catalog, host, clock, workType: "Calibration");
        await using var db = Db(catalog);
        await Generator(db, host, clock).RunBatchAsync(default);
        var order = await db.WorkOrders.SingleAsync();
        var id = order.Id;
        var actor = EquipmentRetirementDomainTests.Actor;
        Assert.Equal(Maintenance.Application.Features.WorkOrders.AssignmentResult.Saved,
            await new WorkOrderAssignment(db).AssignAsync(id, actor, order.RowVersion, actor, default));
        async Task<byte[]> Version()
        { db.ChangeTracker.Clear(); return (await db.WorkOrders.AsNoTracking().SingleAsync()).RowVersion; }
        var workflow = new WorkOrderWorkflow(db, host.Client, Config(host), clock);
        Assert.Equal(Maintenance.Application.Features.WorkOrders.WorkflowStatus.Forbidden,
            await workflow.StartAsync(id, Guid.NewGuid(), await Version(), default));
        Assert.Equal(Maintenance.Application.Features.WorkOrders.WorkflowStatus.Saved,
            await workflow.StartAsync(id, actor, await Version(), default));
        var startedVersion = await Version();
        var photo = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jZ1sAAAAASUVORK5CYII=");
        var failed = new Maintenance.Application.Features.WorkOrders.WorkOrderReport("Failed", "Calibration outside tolerance",
            null, null, photo, "image/png", [new("TEMP", "Temperature", "C", 11, 0, 10)]);
        Assert.Equal(Maintenance.Application.Features.WorkOrders.WorkflowStatus.Saved,
            await workflow.SubmitAsync(id, actor, startedVersion, failed, default));
        Assert.Equal(Maintenance.Application.Features.WorkOrders.WorkflowStatus.Conflict,
            await workflow.SubmitAsync(id, actor, startedVersion, failed, default));
        Assert.Equal(Maintenance.Application.Features.WorkOrders.WorkflowStatus.Conflict,
            await workflow.ReviewAsync(id, actor, await Version(), true, null, default));
        Assert.Equal(Maintenance.Application.Features.WorkOrders.WorkflowStatus.Conflict,
            await workflow.CancelAsync(id, actor, await Version(), "Bypass calibration", default));
        Assert.True((await db.WorkOrders.AsNoTracking().SingleAsync()).IsBlocking);
        clock.Now = clock.Now.AddMinutes(1);
        Assert.Equal(Maintenance.Application.Features.WorkOrders.WorkflowStatus.Saved,
            await workflow.ReviewAsync(id, actor, await Version(), false, "Adjust and measure again", default));
        var passed = failed with { Result = "Passed", Measurements = [new("TEMP", "Temperature", "C", 5, 0, 10)] };
        Assert.Equal(Maintenance.Application.Features.WorkOrders.WorkflowStatus.Saved,
            await workflow.SubmitAsync(id, actor, await Version(), passed, default));
        clock.Now = clock.Now.AddMinutes(1);
        Assert.Equal(Maintenance.Application.Features.WorkOrders.WorkflowStatus.Saved,
            await workflow.ReviewAsync(id, actor, await Version(), true, null, default));
        Assert.Equal("Closed", (await db.WorkOrders.AsNoTracking().SingleAsync()).Status);
        var attempts = await db.WorkOrderAttempts.AsNoTracking().OrderBy(x => x.AttemptNumber).ToArrayAsync();
        Assert.Equal(new[] { "Rejected", "Accepted" }, attempts.Select(x => x.ReviewDecision));
        Assert.All(attempts, x => Assert.Equal(photo, x.OperationPhoto));
        Assert.Equal(new[] { false, true }, await db.CalibrationMeasurements.OrderBy(x => x.MeasuredValue).Select(x => x.Passed).OrderBy(x => x).ToArrayAsync());
        Assert.Equal(1, await db.AuditEntries.CountAsync(x => x.Action == "WorkOrderAccepted"));
        Assert.Equal(7, await db.OutboxMessages.CountAsync(x => x.EventType != UserNotificationEvent.Requested));
        Assert.Equal(4, await db.OutboxMessages.CountAsync(x => x.EventType == UserNotificationEvent.Requested));
        // Deliver terminal event first, then every older event: no resurrection and Incident wins status priority.
        await using var target = host.Context();
        var asset = await target.EquipmentAssets.SingleAsync();
        var incidentProjection = Equipment.Domain.EquipmentStateProjection.ForIncident(Guid.NewGuid(), host.AssetId);
        incidentProjection.ApplyIncident("Open", 1, Guid.NewGuid(), clock.Now, clock.Now);
        target.Add(incidentProjection); asset.ApplyBlockingStatus(true, false, clock.Now);
        await target.SaveChangesAsync();
        await using var services = new ServiceCollection().AddDbContext<Equipment.Infrastructure.Persistence.EquipmentDbContext>(
            options => options.UseSqlServer(target.Database.GetConnectionString())).BuildServiceProvider();
        using var consumer = new Equipment.Infrastructure.Workers.WorkOrderLifecycleConsumer(new ConfigurationBuilder().Build(),
            services.GetRequiredService<IServiceScopeFactory>(), NullLogger<Equipment.Infrastructure.Workers.WorkOrderLifecycleConsumer>.Instance);
        var messages = await db.OutboxMessages.AsNoTracking().Where(x => x.EventType != UserNotificationEvent.Requested)
            .OrderByDescending(x => x.AggregateVersion).ToArrayAsync();
        foreach (var outbox in messages)
        {
            var envelope = new IntegrationEvent(outbox.EventId, outbox.EventType, 1, "Maintenance", outbox.AggregateId,
                outbox.AggregateVersion, outbox.OccurredAtUtc, outbox.CorrelationId, outbox.PayloadJson);
            var entry = new StreamEntry(outbox.AggregateVersion + "-0", [new("event", JsonSerializer.Serialize(envelope))]);
            await consumer.ProcessAsync(entry, default);
            await consumer.ProcessAsync(entry, default);
        }
        target.ChangeTracker.Clear();
        Assert.Equal("Released", (await target.EquipmentUseRegistrations.SingleAsync()).State);
        Assert.Equal("Broken", (await target.EquipmentAssets.SingleAsync()).OperationalStatus);
        Assert.False((await target.EquipmentStateProjections.SingleAsync(x => x.SourceService == "Maintenance")).IsBlocking);
        Assert.Equal(7, await target.InboxMessages.CountAsync());
    }

    [SqlServerFact]
    public async Task Admin_cancel_fences_inflight_register_and_replayed_command_does_not_change_cancel_intent()
    {
        await using var host = await ProtocolHost.CreateAsync();
        await using var catalog = await TestDatabase.CreateAsync("Maintenance");
        var clock = new Clock();
        await SeedAsync(catalog, host, clock);
        var actor = Guid.NewGuid(); var command = Guid.NewGuid();
        var transport = new AfterEquipment(host.Client, async () =>
        {
            await using var db = Db(catalog);
            var operation = await db.IdempotencyRequests.SingleAsync();
            var version = operation.RowVersion.ToArray();
            var admin = new WorkOrderOperationAdmin(db, clock);
            var requested = await admin.ExecuteAsync(operation.OperationId, "Cancel", actor, command, version, "Cancel pending", default);
            Assert.Equal(Maintenance.Application.Features.WorkOrders.OperationCommandStatus.Saved, requested.Status);
            var replay = await admin.ExecuteAsync(operation.OperationId, "Cancel", actor, command, version, "Cancel pending", default);
            Assert.Equal(requested.Value!.RowVersion, replay.Value!.RowVersion);
            var conflict = await admin.ExecuteAsync(operation.OperationId, "Cancel", actor, command, version, "Different payload", default);
            Assert.Equal(Maintenance.Application.Features.WorkOrders.OperationCommandStatus.Conflict, conflict.Status);
        });
        await using (var db = Db(catalog))
        {
            Assert.Equal(0, await Generator(db, host, clock, transport).RunBatchAsync(default));
            Assert.Empty(await db.WorkOrders.ToArrayAsync());
            Assert.Equal(1, await db.AuditEntries.CountAsync(x => x.Action == "CancelRequested"));
            Assert.Equal(1, await db.AuditEntries.CountAsync(x => x.Action == "StaleResultIgnored"));
        }
        clock.Now = clock.Now.AddMinutes(2);
        await using var restart = Db(catalog);
        Assert.Equal(1, await Generator(restart, host, clock).RunBatchAsync(default));
        Assert.Equal("Aborted", (await restart.IdempotencyRequests.SingleAsync()).State);
        await using var equipment = host.Context();
        Assert.Equal("Aborted", (await equipment.EquipmentUseRegistrations.SingleAsync()).State);
    }
}
