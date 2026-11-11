using System.Net.Http.Json;
using System.Text.Json;
using MedEquipSLA.Contracts.Events;
using MedEquipSLA.Maintenance.Infrastructure.Persistence;
using MedEquipSLA.Maintenance.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace MedEquipSLA.Maintenance.Infrastructure.Workers;

public sealed class UserAccessConsumer(IConfiguration config, IServiceScopeFactory scopes, ILogger<UserAccessConsumer> logger) : BackgroundService
{
    private Guid? lastUser;
    public const string Group = "maintenance-user-access-v1";
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!config.GetValue<bool>("Workers:Enabled")) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        var consumer = Guid.NewGuid().ToString("N"); RedisValue cursor = "0-0";
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var redis = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>().GetDatabase();
                try { await redis.StreamCreateConsumerGroupAsync(UserAccessChangedEvent.Stream, Group, "0-0", true); }
                catch (RedisServerException error) when (error.Message.StartsWith("BUSYGROUP", StringComparison.Ordinal)) { }
                var claimed = await redis.StreamAutoClaimAsync(UserAccessChangedEvent.Stream, Group, consumer, 30000, cursor, 20);
                cursor = claimed.NextStartId;
                foreach (var entry in claimed.ClaimedEntries.Concat(await redis.StreamReadGroupAsync(UserAccessChangedEvent.Stream, Group, consumer, ">", 20)))
                {
                    try
                    {
                        var envelope = JsonSerializer.Deserialize<IntegrationEvent>(entry.Values.Single(x => x.Name == "event").Value.ToString());
                        if (envelope is null || envelope.Producer != "Identity" || envelope.EventType != UserAccessChangedEvent.Changed ||
                            envelope.EventId == Guid.Empty || envelope.SchemaVersion != 1) throw new JsonException("Invalid access event.");
                        var payload = JsonSerializer.Deserialize<UserAccessChangedEvent>(envelope.Payload);
                        if (payload is null || payload.UserId != envelope.AggregateId || payload.AuthorizationVersion != envelope.AggregateVersion || payload.AuthorizationVersion < 1) throw new JsonException("Invalid access binding.");
                        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
                            { BaseAddress = new Uri(config["Identity:Address"] ?? "http://localhost:5101"), Timeout = TimeSpan.FromSeconds(3) };
                        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/internal/technician-access/{payload.UserId}");
                        var key = config["Identity:RecipientLookupKey"];
                        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("Access lookup key missing.");
                        request.Headers.Add("X-Recipient-Lookup-Key", key);
                        using var response = await client.SendAsync(request, ct); response.EnsureSuccessStatusCode();
                        var current = await response.Content.ReadFromJsonAsync<AccessReply>(cancellationToken: ct);
                        if (current is null || current.UserId != payload.UserId || current.AuthorizationVersion < payload.AuthorizationVersion)
                            throw new InvalidOperationException("Access state is unresolved.");
                        await ApplyAsync(scope.ServiceProvider.GetRequiredService<MaintenanceDbContext>(), envelope, current.Eligible, current.AuthorizationVersion, ct);
                        await redis.StreamAcknowledgeAsync(UserAccessChangedEvent.Stream, Group, entry.Id);
                    }
                    catch (JsonException)
                    {
                        await redis.StreamAddAsync(UserAccessChangedEvent.Stream + ":" + Group + ":dead-letter", entry.Values);
                        await redis.StreamAcknowledgeAsync(UserAccessChangedEvent.Stream, Group, entry.Id);
                    }
                    catch (Exception error) when (!ct.IsCancellationRequested) { logger.LogWarning(error, "Access change remains pending, entry {Entry}.", entry.Id); }
                }
                await ReconcileAsync(scope.ServiceProvider.GetRequiredService<MaintenanceDbContext>(), ct);
            }
            catch (Exception error) when (!ct.IsCancellationRequested) { logger.LogWarning(error, "User access stream unavailable."); }
        } while (await timer.WaitForNextTickAsync(ct));
    }
    // Covers a permission event consumed before an already-authorized assignment commits.
    // Admission snapshots fence late revocation; this bounded sweep retries current Identity state after restart.
    private async Task ReconcileAsync(MaintenanceDbContext db, CancellationToken ct)
    {
        var users = db.WorkOrders.AsNoTracking().Where(x => x.AssignedTechnicianId != null && x.Status != "Closed" && x.Status != "Cancelled")
            .Select(x => x.AssignedTechnicianId!.Value).Distinct();
        if (lastUser is { } after) users = users.Where(x => x.CompareTo(after) > 0);
        var ids = await users.OrderBy(x => x).Take(20).ToArrayAsync(ct);
        lastUser = ids.Length == 0 ? null : ids[^1];
        foreach (var id in ids)
        {
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
                { BaseAddress = new Uri(config["Identity:Address"] ?? "http://localhost:5101"), Timeout = TimeSpan.FromSeconds(3) };
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/internal/technician-access/{id}");
            var key = config["Identity:RecipientLookupKey"];
            if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("Access lookup key missing.");
            request.Headers.Add("X-Recipient-Lookup-Key", key);
            using var response = await client.SendAsync(request, ct); response.EnsureSuccessStatusCode();
            var current = await response.Content.ReadFromJsonAsync<AccessReply>(cancellationToken: ct);
            if (current is null || current.UserId != id || current.AuthorizationVersion < 1) throw new InvalidOperationException("Unresolved technician.");
            if (current.Eligible) continue;
            var envelope = new IntegrationEvent(Guid.NewGuid(), UserAccessChangedEvent.Changed, 1, "Identity", id, current.AuthorizationVersion,
                DateTimeOffset.UtcNow, Guid.NewGuid().ToString(), JsonSerializer.Serialize(new UserAccessChangedEvent(id, current.AuthorizationVersion)));
            await ApplyAsync(db, envelope, false, current.AuthorizationVersion, ct, recordInbox: false);
        }
    }
    private sealed record AccessReply(Guid UserId, bool Eligible, long AuthorizationVersion);
    public static async Task ApplyAsync(MaintenanceDbContext db, IntegrationEvent envelope, bool eligible, long currentVersion, CancellationToken ct, bool recordInbox = true)
    {
        var payload = JsonSerializer.Deserialize<UserAccessChangedEvent>(envelope.Payload) ?? throw new JsonException();
        if (envelope.Producer != "Identity" || payload.UserId != envelope.AggregateId || currentVersion < payload.AuthorizationVersion) throw new InvalidOperationException("Access proof mismatch.");
        db.ChangeTracker.Clear();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var resource = "UserAccessEvent:" + envelope.EventId.ToString("N");
        await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={resource},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 51073,'Access event lock timeout',1", ct);
        if (recordInbox && await db.InboxMessages.AnyAsync(x => x.ConsumerName == Group && x.EventId == envelope.EventId, ct)) return;
        if (!eligible)
        {
            var candidates = await db.WorkOrders.AsNoTracking().Where(x => x.AssignedTechnicianId == payload.UserId && x.AssignedAuthorizationVersion < currentVersion && x.Status != "Closed" && x.Status != "Cancelled")
                .OrderBy(x => x.EquipmentId).ThenBy(x => x.Id).Select(x => new { x.EquipmentId, x.Id }).ToArrayAsync(ct);
            foreach (var candidate in candidates)
            {
                await EquipmentGateAccess.LockAsync(db, candidate.EquipmentId, ct);
                var row = await db.WorkOrders.FromSqlInterpolated($"SELECT * FROM dbo.WorkOrders WITH (UPDLOCK,HOLDLOCK) WHERE Id={candidate.Id}").SingleAsync(ct);
                var now = DateTimeOffset.UtcNow;
                if (!row.ClearIneligibleAssignment(payload.UserId, currentVersion, now)) continue;
                db.Add(MedEquipSLA.Maintenance.Domain.WorkOrderActivity.Transition(row.Id, "AssignmentRevoked", row.Status, row.Status, payload.UserId, "Identity access changed", now));
                db.Add(AuditEntry.ForOperation(row.Id, "AssignmentRevoked", now, envelope.CorrelationId, JsonSerializer.Serialize(new { payload.UserId, currentVersion })));
                db.Add(new OutboxMessage(new UserNotificationEvent("WorkOrderAssigned", "Admins", null, "WorkOrder", row.Id,
                    row.DepartmentIdSnapshot, "Cần phân công lại công việc", $"Kỹ thuật viên không còn quyền xử lý {row.Code}.")
                    .Envelope("Maintenance", row.AggregateVersion, now, envelope.CorrelationId), "WorkOrder"));
            }
        }
        if (recordInbox) db.Add(new InboxMessage(Group, envelope.EventId, envelope.EventType, 1, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
}
