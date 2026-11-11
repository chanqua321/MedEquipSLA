using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Grpc.Core;
using MedEquipSLA.Application.Coordination;
using MedEquipSLA.Contracts.Grpc.Equipment.V1;
using MedEquipSLA.Equipment.Application.Features.Retirement;
using MedEquipSLA.Equipment.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static MedEquipSLA.Database.IntegrationTests.EquipmentWorkOrderGrpcTests;

namespace MedEquipSLA.Database.IntegrationTests;

public sealed class EquipmentRetirementProtocolTests
{
    private static async Task<(RetirementView Value, Guid Key)> BeginAsync(ProtocolHost host, HttpClient http, Guid? key = null)
    {
        var k = key ?? Guid.NewGuid();
        using var message = new HttpRequestMessage(HttpMethod.Post, $"/api/equipment/{host.AssetId}/retirement-intents")
        { Version = HttpVersion.Version20, VersionPolicy = HttpVersionPolicy.RequestVersionExact,
            Content = JsonContent.Create(new { reason = "Replace obsolete device" }) };
        message.Headers.Add("Idempotency-Key", k.ToString());
        using var response = await http.SendAsync(message);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        return ((await response.Content.ReadFromJsonAsync<RetirementView>())!, k);
    }
    private static async Task<(HttpStatusCode Status, RetirementView? Value)> CommandAsync(ProtocolHost host, HttpClient http,
        RetirementView view, string action, Guid? key = null)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post,
            $"/api/equipment/{host.AssetId}/retirement-intents/{view.Id}/{action}")
        { Version = HttpVersion.Version20, VersionPolicy = HttpVersionPolicy.RequestVersionExact,
            Content = JsonContent.Create(new { reason = "Reviewed by administrator", rowVersion = view.RowVersion }) };
        message.Headers.Add("Idempotency-Key", (key ?? Guid.NewGuid()).ToString());
        using var response = await http.SendAsync(message);
        return (response.StatusCode, response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<RetirementView>() : null);
    }
    private static async Task<RetirementView> GetAsync(ProtocolHost host, HttpClient http, Guid intent) =>
        (await http.GetFromJsonAsync<RetirementView>($"/api/equipment/{host.AssetId}/retirement-intents/{intent}"))!;
    private static RetirementSourceReport Report(ProtocolHost host, RetirementView intent, string state = "Sealed",
        long version = 1) => new()
    {
        EquipmentId = host.AssetId.ToString(), IntentId = intent.Id.ToString(), RetirementEpoch = intent.Epoch,
        CoverageVersion = 1, SourceGateVersion = version, SourceState = state,
        ProofId = state is "Sealed" or "CancelAck" ? Guid.NewGuid().ToString() : "",
        CorrelationId = Guid.NewGuid().ToString()
    };

    [SqlServerFact]
    public async Task Retirement_credentials_are_bound_to_source_and_cannot_invoke_admin_or_workorder_methods()
    {
        await using var host = await ProtocolHost.CreateAsync();
        foreach (var credentials in new[] { new Metadata(), host.Credentials,
            new Metadata { { "authorization", "Bearer " + host.AdminToken() } },
            new Metadata { { "x-retirement-source", "Incident" },
                { "x-retirement-source-key", host.RetirementCredentials("Maintenance").GetValue("x-retirement-source-key")! } } })
        {
            var denied = await Assert.ThrowsAsync<RpcException>(async () =>
                await host.Client.ListRetirementIntentsAsync(new() { Page = 1 }, credentials));
            Assert.Equal(StatusCode.Unauthenticated, denied.StatusCode);
        }
        Assert.Empty((await host.Client.ListRetirementIntentsAsync(new() { Page = 1 },
            host.RetirementCredentials("Maintenance"))).Intents);
        var workOrderDenied = await Assert.ThrowsAsync<RpcException>(async () =>
            await host.Client.RegisterWorkOrderAsync(host.Request(), host.RetirementCredentials("Maintenance")));
        Assert.Equal(StatusCode.Unauthenticated, workOrderDenied.StatusCode);
        using var http = host.Http();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", host.AdminToken("Technician"));
        using var response = await http.PostAsJsonAsync($"/api/equipment/{host.AssetId}/retirement-intents", new { reason = "Retire" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        http.DefaultRequestHeaders.Authorization = null;
        http.DefaultRequestHeaders.Add("x-retirement-source", "Maintenance");
        http.DefaultRequestHeaders.Add("x-retirement-source-key", host.RetirementCredentials("Maintenance").GetValue("x-retirement-source-key"));
        using var sourceDenied = await http.PostAsJsonAsync($"/api/equipment/{host.AssetId}/retirement-intents", new { reason = "Retire" });
        Assert.Equal(HttpStatusCode.Unauthorized, sourceDenied.StatusCode);
    }

    [SqlServerFact]
    public async Task Two_real_source_databases_seal_before_equipment_can_retire_and_remain_closed_after_restart()
    {
        await using var host = await ProtocolHost.CreateAsync();
        using var http = host.Http();
        http.DefaultRequestHeaders.Authorization = new("Bearer", host.AdminToken());
        var (intent, key) = await BeginAsync(host, http);
        var (replay, _) = await BeginAsync(host, http, key);
        Assert.Equal(intent.Id, replay.Id);
        var noProof = await CommandAsync(host, http, intent, "continue");
        Assert.Equal(HttpStatusCode.Accepted, noProof.Status);
        Assert.Equal("Coordinating", noProof.Value!.State);
        await using var maintenance = await TestDatabase.CreateAsync("Maintenance");
        await using var incident = await TestDatabase.CreateAsync("Incident");
        foreach (var (owner, connection) in new[] { ("Maintenance", maintenance.ConnectionString), ("Incident", incident.ConnectionString) })
        {
            await using var db = Contexts.Create(owner, connection);
            await db.Database.MigrateAsync();
            ISourceRetirementGate gate = db is Maintenance.Infrastructure.Persistence.MaintenanceDbContext m
                ? new Maintenance.Infrastructure.Persistence.SourceRetirementGate(m, TimeProvider.System)
                : new Incident.Infrastructure.Persistence.SourceRetirementGate((Incident.Infrastructure.Persistence.IncidentDbContext)db, TimeProvider.System);
            var sealedSource = await gate.PrepareAsync(host.AssetId, intent.Id, intent.Epoch, default);
            var report = Report(host, intent, version: sealedSource.GateVersion);
            report.ProofId = sealedSource.ReadyProofId!.Value.ToString();
            var reply = await host.Client.ReportRetirementSourceStateAsync(report, host.RetirementCredentials(owner));
            var duplicate = await host.Client.ReportRetirementSourceStateAsync(report, host.RetirementCredentials(owner));
            Assert.Equal(reply.State, duplicate.State);
        }
        var ready = await GetAsync(host, http, intent.Id);
        Assert.Equal("Ready", ready.State);
        Assert.Equal(HttpStatusCode.Conflict, (await CommandAsync(host, http, intent, "continue")).Status);
        var commandKey = Guid.NewGuid();
        var completed = await CommandAsync(host, http, ready, "continue", commandKey);
        Assert.Equal(HttpStatusCode.OK, completed.Status);
        Assert.Equal("Completed", completed.Value!.State);
        Assert.Equal(HttpStatusCode.OK, (await CommandAsync(host, http, ready, "continue", commandKey)).Status);
        await using (var db = host.Context())
        {
            Assert.True((await db.EquipmentAssets.SingleAsync()).IsRetired);
            Assert.Equal(1, await db.EquipmentStatusHistories.CountAsync(x => x.ChangeKind == "Retired"));
            Assert.Equal(1, await db.AuditEntries.CountAsync(x => x.Action == "EquipmentRetired"));
        }
        // Source restart does not reopen the admission barrier before or after observing Completed.
        await using var restarted = (Incident.Infrastructure.Persistence.IncidentDbContext)Contexts.Create("Incident", incident.ConnectionString);
        var coordinator = new Incident.Infrastructure.Persistence.SourceRetirementGate(restarted, TimeProvider.System);
        Assert.Equal("Sealed", (await coordinator.PrepareAsync(host.AssetId, intent.Id, intent.Epoch, default)).Phase);
        Assert.Equal("Retired", (await coordinator.ObserveEquipmentDecisionAsync(host.AssetId, intent.Id, intent.Epoch, "Completed", default)).Phase);
        Assert.Empty((await host.Client.ListRetirementIntentsAsync(new() { Page = 1 }, host.RetirementCredentials("Incident"))).Intents);
    }

    [SqlServerFact]
    public async Task Pending_registration_blocks_final_retire_and_cancellation_needs_both_source_acknowledgements()
    {
        await using var host = await ProtocolHost.CreateAsync();
        await host.Client.RegisterWorkOrderAsync(host.Request(), host.Credentials);
        using var http = host.Http();
        http.DefaultRequestHeaders.Authorization = new("Bearer", host.AdminToken());
        var (intent, _) = await BeginAsync(host, http);
        foreach (var source in new[] { "Maintenance", "Incident" })
            await host.Client.ReportRetirementSourceStateAsync(Report(host, intent), host.RetirementCredentials(source));
        var ready = await GetAsync(host, http, intent.Id);
        var blocked = await CommandAsync(host, http, ready, "continue");
        Assert.Equal(HttpStatusCode.Accepted, blocked.Status);
        Assert.Equal("Ready", blocked.Value!.State);
        await using (var db = host.Context()) Assert.False((await db.EquipmentAssets.SingleAsync()).IsRetired);
        var cancelling = await CommandAsync(host, http, blocked.Value, "cancel");
        Assert.Equal("Cancelling", cancelling.Value!.State);
        var first = await host.Client.ReportRetirementSourceStateAsync(Report(host, intent, "CancelAck", 2), host.RetirementCredentials("Maintenance"));
        Assert.Equal("Cancelling", first.State);
        var ack = Report(host, intent, "CancelAck", 2);
        var last = await host.Client.ReportRetirementSourceStateAsync(ack, host.RetirementCredentials("Incident"));
        Assert.Equal("Cancelled", last.State);
        Assert.Equal("Cancelled", (await host.Client.ReportRetirementSourceStateAsync(ack, host.RetirementCredentials("Incident"))).State);
        var stale = await Assert.ThrowsAsync<RpcException>(async () =>
            await host.Client.ReportRetirementSourceStateAsync(Report(host, intent), host.RetirementCredentials("Incident")));
        Assert.Equal(StatusCode.FailedPrecondition, stale.StatusCode);
        await using (var db = host.Context())
        {
            Assert.False((await db.EquipmentAssets.SingleAsync()).IsRetired);
            Assert.Equal("Pending", (await db.EquipmentUseRegistrations.SingleAsync()).State);
        }
    }

    [SqlServerFact]
    public async Task Concurrent_admin_cancel_and_final_retire_share_one_equipment_lock_and_stale_version_cannot_win()
    {
        await using var host = await ProtocolHost.CreateAsync();
        using var http = host.Http();
        http.DefaultRequestHeaders.Authorization = new("Bearer", host.AdminToken());
        var (intent, _) = await BeginAsync(host, http);
        foreach (var source in new[] { "Maintenance", "Incident" })
            await host.Client.ReportRetirementSourceStateAsync(Report(host, intent), host.RetirementCredentials(source));
        var ready = await GetAsync(host, http, intent.Id);
        var outcomes = await Task.WhenAll(CommandAsync(host, http, ready, "continue"), CommandAsync(host, http, ready, "cancel"));
        Assert.Single(outcomes, x => x.Status == HttpStatusCode.Conflict);
        Assert.Single(outcomes, x => x.Status is HttpStatusCode.OK or HttpStatusCode.Accepted);
        await using var db = host.Context();
        var asset = await db.EquipmentAssets.SingleAsync();
        var persisted = await db.EquipmentRetirementIntents.SingleAsync();
        Assert.Contains(persisted.State, new[] { "Completed", "Cancelling" });
        Assert.Equal(persisted.State == "Completed", asset.IsRetired);
        Assert.Equal(asset.IsRetired ? 1 : 0, await db.EquipmentStatusHistories.CountAsync(x => x.ChangeKind == "Retired"));
        // A new registration cannot slip through either an active intent or a terminal Retired asset.
        var denied = await Assert.ThrowsAsync<RpcException>(async () =>
            await host.Client.RegisterWorkOrderAsync(host.Request(), host.Credentials));
        Assert.Equal(StatusCode.FailedPrecondition, denied.StatusCode);
    }

    [SqlServerFact]
    public async Task Audit_failure_rolls_back_equipment_intent_history_and_idempotency_together()
    {
        await using var host = await ProtocolHost.CreateAsync();
        using var http = host.Http();
        http.DefaultRequestHeaders.Authorization = new("Bearer", host.AdminToken());
        var (intent, _) = await BeginAsync(host, http);
        foreach (var source in new[] { "Maintenance", "Incident" })
            await host.Client.ReportRetirementSourceStateAsync(Report(host, intent), host.RetirementCredentials(source));
        var ready = await GetAsync(host, http, intent.Id);
        await using (var db = host.Context())
        {
            await db.Database.ExecuteSqlRawAsync("""
                ALTER TABLE dbo.AuditEntries ADD CONSTRAINT CK_Test_RetireFailure CHECK(Action<>N'EquipmentRetired')
                """);
            await Assert.ThrowsAsync<DbUpdateException>(() =>
                new RetirementStore(db).AdminAsync(host.AssetId, intent.Id, EquipmentRetirementDomainTests.Actor,
                    Guid.NewGuid(), ready.RowVersion, "Continue", "Reviewed", "test-rollback", default));
        }
        await using (var db = host.Context())
        {
            Assert.False((await db.EquipmentAssets.SingleAsync()).IsRetired);
            Assert.Equal("Ready", (await db.EquipmentRetirementIntents.SingleAsync()).State);
            Assert.Equal(0, await db.EquipmentStatusHistories.CountAsync(x => x.ChangeKind == "Retired"));
            Assert.Equal(0, await db.IdempotencyRequests.CountAsync(x => x.OperationName == "ContinueRetirement"));
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.AuditEntries DROP CONSTRAINT CK_Test_RetireFailure");
        }
        Assert.Equal(HttpStatusCode.OK, (await CommandAsync(host, http, ready, "continue")).Status);
    }
}
