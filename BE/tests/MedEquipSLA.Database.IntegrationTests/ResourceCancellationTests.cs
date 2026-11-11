using System.Security.Cryptography;
using System.Text;
using Grpc.Core;
using MedEquipSLA.Application.Coordination;
using MedEquipSLA.Contracts.Grpc.Equipment.V1;
using MedEquipSLA.Maintenance.Domain;
using MedEquipSLA.Maintenance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;
using static MedEquipSLA.Database.IntegrationTests.EquipmentWorkOrderGrpcTests;

namespace MedEquipSLA.Database.IntegrationTests;

public sealed class ResourceCancellationTests
{
    private sealed class Clock : TimeProvider
    {
        internal DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class LoseAbortReply(EquipmentLookup.EquipmentLookupClient real) : EquipmentLookup.EquipmentLookupClient
    {
        public override AsyncUnaryCall<ResourceUseReply> AbortResourceUseAsync(MedEquipSLA.Contracts.Grpc.Equipment.V1.ResourceUseAbortRequest request,
            Metadata headers = null!, DateTime? deadline = null, CancellationToken cancellationToken = default)
        {
            var call = real.AbortResourceUseAsync(request, headers, deadline, cancellationToken);
            async Task<ResourceUseReply> Reply()
            { await call; throw new RpcException(new(StatusCode.DeadlineExceeded, "Injected lost abort response")); }
            return new(Reply(), call.ResponseHeadersAsync, call.GetStatus, call.GetTrailers, call.Dispose);
        }
    }
    private static IConfiguration Config(ProtocolHost host, string source) => new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string?> { ["Grpc:RetirementSourceKey"] = host.RetirementCredentials(source).GetValue("x-retirement-source-key"),
            ["Grpc:MaintenanceWorkOrderKey"] = host.Credentials.GetValue("x-maintenance-workorder-key") }).Build();
    private static ISourceCreationOperations Store(DbContext db, string owner, ProtocolHost host, Clock clock,
        EquipmentLookup.EquipmentLookupClient? client = null) =>
        owner == "Maintenance" ? new SourceCreationOperations((MaintenanceDbContext)db, client ?? host.Client, Config(host, owner), clock) :
        new Incident.Infrastructure.Persistence.SourceCreationOperations((Incident.Infrastructure.Persistence.IncidentDbContext)db,
            client ?? host.Client, Config(host, owner), clock);
    private static async Task<Guid> SeedAsync(DbContext db, string owner, Guid equipment, Clock clock)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("verified request payload"));
        if (owner == "Maintenance")
        {
            var op = Maintenance.Infrastructure.Persistence.Entities.IdempotencyRequest.Begin(Guid.NewGuid(), Guid.NewGuid(), hash, clock.Now);
            op.BindPendingRequest(equipment, "{}", hash); db.Add(op); await db.SaveChangesAsync(); return op.OperationId;
        }
        else
        {
            var op = Incident.Infrastructure.Persistence.Entities.IdempotencyRequest.Begin(Guid.NewGuid().ToString(), "CreateIncident",
                Guid.NewGuid().ToString(), hash, clock.Now);
            op.BindPendingRequest(equipment, "{}", hash); db.Add(op); await db.SaveChangesAsync(); return op.OperationId;
        }
    }

    [SqlServerTheory]
    [InlineData("Maintenance", false)]
    [InlineData("Maintenance", true)]
    [InlineData("Incident", false)]
    [InlineData("Incident", true)]
    public async Task Admin_cancellation_is_idempotent_and_abort_tombstone_blocks_late_registration(string owner, bool registered)
    {
        await using var host = await ProtocolHost.CreateAsync();
        await using var catalog = await TestDatabase.CreateAsync(owner);
        var clock = new Clock();
        await using var source = Contexts.Create(owner, catalog.ConnectionString);
        await source.Database.MigrateAsync();
        var id = await SeedAsync(source, owner, host.AssetId, clock);
        if (registered)
        {
            var request = new RegisterIncidentUseRequest { EquipmentId = host.AssetId.ToString(), OperationId = id.ToString() };
            if (owner == "Maintenance") await host.Client.RegisterScheduleUseAsync(request, host.RegistrationCredentials(owner));
            else await host.Client.RegisterIncidentUseAsync(request, host.RegistrationCredentials(owner));
        }
        var store = Store(source, owner, host, clock);
        var before = (await store.FindAsync(id, default))!;
        var command = new SourceOperationCommand("Cancel", Guid.NewGuid(), Guid.NewGuid(), before.RowVersion, "Cancel uncommitted request");
        Assert.Equal("Saved", (await store.ExecuteAsync(id, command, default)).Status);
        Assert.Equal("Saved", (await store.ExecuteAsync(id, command, default)).Status);
        Assert.Equal("Conflict", (await store.ExecuteAsync(id, command with { Reason = "Different payload" }, default)).Status);
        Assert.Equal(1, await store.ReconcileCancellationsAsync(default));
        Assert.Equal("Aborted", (await store.FindAsync(id, default))!.State);
        await using var equipment = host.Context();
        var registration = await equipment.EquipmentUseRegistrations.SingleAsync();
        Assert.Equal("Aborted", registration.State);
        Assert.NotNull(registration.CancelIntentId);
        var lateRequest = new RegisterIncidentUseRequest { EquipmentId = host.AssetId.ToString(), OperationId = id.ToString() };
        var denied = await Assert.ThrowsAsync<RpcException>(async () =>
        {
            if (owner == "Maintenance") await host.Client.RegisterScheduleUseAsync(lateRequest, host.RegistrationCredentials(owner));
            else await host.Client.RegisterIncidentUseAsync(lateRequest, host.RegistrationCredentials(owner));
        });
        Assert.Equal(StatusCode.FailedPrecondition, denied.StatusCode);
        source.ChangeTracker.Clear();
        ISourceRetirementGate gate = source is MaintenanceDbContext maintenance
            ? new SourceRetirementGate(maintenance, clock) : new Incident.Infrastructure.Persistence.SourceRetirementGate(
                (Incident.Infrastructure.Persistence.IncidentDbContext)source, clock);
        Assert.Equal("Sealed", (await gate.PrepareAsync(host.AssetId, Guid.NewGuid(), 1, default)).Phase);
    }

    [SqlServerTheory]
    [InlineData("Maintenance")]
    [InlineData("Incident")]
    public async Task Lost_abort_reply_keeps_source_pending_then_restart_accepts_durable_terminal_proof(string owner)
    {
        await using var host = await ProtocolHost.CreateAsync();
        await using var catalog = await TestDatabase.CreateAsync(owner);
        var clock = new Clock();
        Guid id;
        await using (var db = Contexts.Create(owner, catalog.ConnectionString))
        {
            await db.Database.MigrateAsync();
            id = await SeedAsync(db, owner, host.AssetId, clock);
            var store = Store(db, owner, host, clock, new LoseAbortReply(host.Client));
            var view = (await store.FindAsync(id, default))!;
            await store.ExecuteAsync(id, new("Cancel", Guid.NewGuid(), Guid.NewGuid(), view.RowVersion, "Cancel"), default);
            Assert.Equal(0, await store.ReconcileCancellationsAsync(default));
            Assert.Equal("Pending", (await store.FindAsync(id, default))!.State);
        }
        await using (var equipment = host.Context())
            Assert.Equal("Aborted", (await equipment.EquipmentUseRegistrations.SingleAsync()).State);
        clock.Now = clock.Now.AddMinutes(2);
        await using var restart = Contexts.Create(owner, catalog.ConnectionString);
        Assert.Equal(1, await Store(restart, owner, host, clock).ReconcileCancellationsAsync(default));
        Assert.Equal("Aborted", (await Store(restart, owner, host, clock).FindAsync(id, default))!.State);
    }

    [SqlServerFact]
    public async Task Permanent_end_preserves_occurrence_times_prevents_reactivation_and_releases_only_after_pending_resolves()
    {
        await using var host = await ProtocolHost.CreateAsync();
        await using var catalog = await TestDatabase.CreateAsync("Maintenance");
        var clock = new Clock();
        var id = Guid.NewGuid();
        var remote = await host.Client.RegisterScheduleUseAsync(new() { EquipmentId = host.AssetId.ToString(), OperationId = id.ToString() },
            host.RegistrationCredentials("Maintenance"));
        await using var source = (MaintenanceDbContext)Contexts.Create("Maintenance", catalog.ConnectionString);
        await source.Database.MigrateAsync();
        var schedule = MaintenanceSchedule.Create(id, host.AssetId, remote.EquipmentCode, remote.EquipmentName,
            Guid.Parse(remote.DepartmentId), remote.DepartmentName, Guid.Parse(remote.RegistrationId), "Maintenance",
            clock.Now.AddMinutes(30), 7, 60, 30, true, Guid.NewGuid(), clock.Now.AddDays(-1));
        var occurrence = schedule.PlanNext(clock.Now); var originalTime = occurrence.ScheduledOccurrenceAtUtc;
        source.AddRange(schedule, occurrence); await source.SaveChangesAsync();
        var pendingId = await SeedAsync(source, "Maintenance", host.AssetId, clock);
        var termination = new ScheduleTermination(source, host.Client, Config(host, "Maintenance"), clock, new());
        var command = Guid.NewGuid(); var actor = Guid.NewGuid(); var version = schedule.RowVersion.ToArray();
        Assert.Equal(Maintenance.Application.Features.Schedules.ScheduleStatus.Saved,
            (await termination.ExecuteAsync(id, actor, command, version, "Decommission schedule", false, default)).Status);
        Assert.Equal(Maintenance.Application.Features.Schedules.ScheduleStatus.Saved,
            (await termination.ExecuteAsync(id, actor, command, version, "Decommission schedule", false, default)).Status);
        Assert.Equal(0, await termination.ReleaseBatchAsync(default));
        source.ChangeTracker.Clear();
        var savedOccurrence = await source.ScheduleOccurrences.SingleAsync();
        Assert.Equal("Cancelled", savedOccurrence.State); Assert.Equal(originalTime, savedOccurrence.ScheduledOccurrenceAtUtc);
        var savedSchedule = await source.MaintenanceSchedules.SingleAsync();
        Assert.Throws<InvalidOperationException>(() => savedSchedule.Revise(savedSchedule.AnchorAtUtc, savedSchedule.NextOccurrenceAtUtc,
            7, 60, 30, true, actor, clock.Now));
        var pendingStore = Store(source, "Maintenance", host, clock);
        var pending = (await pendingStore.FindAsync(pendingId, default))!;
        await pendingStore.ExecuteAsync(pendingId, new("Cancel", actor, Guid.NewGuid(), pending.RowVersion, "Cancel pending"), default);
        Assert.Equal(1, await pendingStore.ReconcileCancellationsAsync(default));
        // Cursor may have reached the end on its prior blocked scan; next scan wraps fairly.
        var released = await termination.ReleaseBatchAsync(default);
        if (released == 0) released = await termination.ReleaseBatchAsync(default);
        Assert.Equal(1, released);
        source.ChangeTracker.Clear();
        Assert.NotNull((await source.MaintenanceSchedules.SingleAsync()).RegistrationReleasedAtUtc);
        await using var equipment = host.Context();
        Assert.Equal("Released", (await equipment.EquipmentUseRegistrations.SingleAsync(x => x.Id == Guid.Parse(remote.RegistrationId))).State);
    }
}
