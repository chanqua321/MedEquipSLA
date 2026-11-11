using System.Text.Json;
using MedEquipSLA.Contracts.Events;
using MedEquipSLA.Equipment.Domain;
using MedEquipSLA.Equipment.Infrastructure.Persistence;
using MedEquipSLA.Equipment.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace MedEquipSLA.Equipment.Infrastructure.Workers;

public sealed class RegistrationSnapshotConsumer(IConfiguration config, IServiceScopeFactory scopes,
    ILogger<RegistrationSnapshotConsumer> logger) : BackgroundService
{
    public const string Group = "equipment-registration-snapshots-v1";
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!config.GetValue<bool>("Workers:Enabled")) return;
        var consumer = Guid.NewGuid().ToString("N"); RedisValue cursor = "0-0";
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var redis = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>().GetDatabase();
                try { await redis.StreamCreateConsumerGroupAsync(RegistrationSnapshotPage.Stream, Group, "0-0", true); }
                catch (RedisServerException e) when (e.Message.StartsWith("BUSYGROUP", StringComparison.Ordinal)) { }
                var retry = await redis.StreamAutoClaimAsync(RegistrationSnapshotPage.Stream, Group, consumer, 30000, cursor, count: 10);
                cursor = retry.NextStartId;
                var fresh = await redis.StreamReadGroupAsync(RegistrationSnapshotPage.Stream, Group, consumer, ">", count: 10);
                foreach (var entry in retry.ClaimedEntries.Concat(fresh))
                {
                    IntegrationEvent? message = null;
                    try
                    {
                        var raw = entry.Values.FirstOrDefault(x => x.Name == "event").Value;
                        if (raw.IsNull) throw new JsonException("Missing snapshot envelope.");
                        message = JsonSerializer.Deserialize<IntegrationEvent>(raw.ToString())
                            ?? throw new JsonException("Missing snapshot envelope.");
                        await ProcessAsync(message, ct);
                        await redis.StreamAcknowledgeAsync(RegistrationSnapshotPage.Stream, Group, entry.Id);
                    }
                    catch (JsonException)
                    {
                        if (message is not null) await RecordFailureAsync(message,"InvalidSnapshot",true,ct);
                        await redis.StreamAddAsync(RegistrationSnapshotPage.Stream+":dead-letter", entry.Values);
                        await redis.StreamAcknowledgeAsync(RegistrationSnapshotPage.Stream, Group, entry.Id);
                    }
                    catch (SnapshotPageNotReadyException)
                    { logger.LogInformation("Snapshot page {EntryId} waits for prior durable pages.",entry.Id); }
                    catch (Exception e) when (!ct.IsCancellationRequested)
                    {
                        var pending = await redis.StreamPendingMessagesAsync(RegistrationSnapshotPage.Stream,Group,1,RedisValue.Null,entry.Id,entry.Id);
                        var exhausted = pending is [{ DeliveryCount: >= 8 }];
                        if (message is not null) await RecordFailureAsync(message,e.GetType().Name,exhausted,ct);
                        if (exhausted)
                        {
                            await redis.StreamAddAsync(RegistrationSnapshotPage.Stream+":dead-letter",entry.Values);
                            await redis.StreamAcknowledgeAsync(RegistrationSnapshotPage.Stream,Group,entry.Id);
                        }
                        logger.LogWarning(e, "Snapshot page {EntryId} failed; exhausted {Exhausted}; no absence-based release.", entry.Id,exhausted);
                    }
                }
            }
            catch (Exception e) when (!ct.IsCancellationRequested) { logger.LogWarning(e, "Registration snapshot stream unavailable."); }
        } while (await timer.WaitForNextTickAsync(ct));
    }

    private async Task RecordFailureAsync(IntegrationEvent message,string error,bool exhausted,CancellationToken ct)
    {
        if (message.Producer is not ("Incident" or "Maintenance") || message.EventType != RegistrationSnapshotPage.EventType) return;
        RegistrationSnapshotPage? page;
        try { page = JsonSerializer.Deserialize<RegistrationSnapshotPage>(message.Payload);page?.Validate(); }
        catch(JsonException) { return; }
        if(page is null || page.SnapshotId!=message.AggregateId) return;
        await using var scope=scopes.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<EquipmentDbContext>();
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        var key="RegistrationSnapshot:"+message.Producer;
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @result int;
            EXEC @result=sp_getapplock @Resource={key},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=5000;
            IF @result<0 THROW 51076,'Snapshot checkpoint lock unavailable.',1;
            """,ct);
        var row=await db.ReconciliationCheckpoints.SingleOrDefaultAsync(x=>x.SourceService==message.Producer,ct);
        if(row is null) { row=ReconciliationCheckpoint.ForSource(message.Producer);db.Add(row); }
        row.RecordFailure(page.SnapshotId,page.Watermark,page.CapturedAtUtc,error,exhausted);
        db.Add(AuditEntry.ForService(message.Producer,exhausted ? "RegistrationSnapshotFailed" : "RegistrationSnapshotRetry",
            "ReconciliationCheckpoint",page.SnapshotId,DateTimeOffset.UtcNow,message.CorrelationId,
            JsonSerializer.Serialize(new { page.Index,error,exhausted })));
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
    }

    public async Task ProcessAsync(IntegrationEvent message, CancellationToken ct)
    {
        if (message.Producer is not ("Incident" or "Maintenance") || message.EventType != RegistrationSnapshotPage.EventType ||
            message.SchemaVersion != 1 || message.EventId == Guid.Empty) throw new JsonException("Invalid snapshot producer/envelope.");
        var page = JsonSerializer.Deserialize<RegistrationSnapshotPage>(message.Payload) ?? throw new JsonException("Missing page.");
        page.Validate();
        if (message.AggregateId != page.SnapshotId || message.AggregateVersion != page.Index+1 || message.EventId != page.Envelope(message.Producer).EventId ||
            message.OccurredAtUtc != page.CapturedAtUtc) throw new JsonException("Snapshot envelope binding mismatch.");
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<EquipmentDbContext>();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var key = "RegistrationSnapshot:"+message.Producer;
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @result int;
            EXEC @result=sp_getapplock @Resource={key},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=5000;
            IF @result<0 THROW 51076,'Snapshot checkpoint lock unavailable.',1;
            """, ct);
        var checkpoint = await db.ReconciliationCheckpoints.SingleOrDefaultAsync(x => x.SourceService == message.Producer, ct);
        if (checkpoint is null) { checkpoint = ReconciliationCheckpoint.ForSource(message.Producer); db.Add(checkpoint); }
        if (!checkpoint.Admit(page.SnapshotId, page.Watermark, page.CapturedAtUtc, page.Index)) return;
        // Apply each positive proof in its existing idempotent local transaction, then advance the cursor.
        // If this process dies before checkpoint commit, Inbox/version guards make the page replay safe.
        var unresolved = 0;
        foreach (var (item, index) in page.Items.Select((item,index) => (item,index)))
        {
            var owned = message.Producer == "Incident" ? item.Kind == "Incident" : item.Kind is "WorkOrder" or "MaintenanceSchedule";
            if (!owned) throw new JsonException("Snapshot item belongs to another source.");
            if (item.State == "Aborted") continue; // Terminal inventory entry is not an instruction to mutate Equipment.
            if (item.State != "Committed" || item.EquipmentId is null || item.RegistrationId is null || item.Version < 1)
            { unresolved++; continue; } // Unknown/Pending/Aborted is inventory evidence, never a release instruction.
            if (item.Kind == "Incident" && item.Status is not ("Open" or "InProgress" or "Resolved" or "Closed") ||
                item.Kind == "MaintenanceSchedule" && (item.Status is not ("Active" or "Inactive" or "Ended") || item.ScheduleReleaseConfirmed && item.Status != "Ended"))
                throw new JsonException("Invalid source state proof.");
            var eventId = new Guid(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(
                $"{page.SnapshotId:N}:{page.Index}:{index}")).AsSpan(0,16));
            if (item.Kind == "Incident")
            {
                var payload = new IncidentLifecycleEvent(item.EquipmentId.Value,item.RegistrationId.Value,item.Status == "Closed" ? "Closed" : "Open");
                var envelope = new IntegrationEvent(eventId, item.Status == "Closed" ? IncidentLifecycleEvent.Closed : IncidentLifecycleEvent.Created,
                    1,"Incident",item.ResourceId,item.Version,page.CapturedAtUtc,message.CorrelationId,JsonSerializer.Serialize(payload));
                using var consumer = new IncidentLifecycleConsumer(config,scopes,Microsoft.Extensions.Logging.Abstractions.NullLogger<IncidentLifecycleConsumer>.Instance);
                await consumer.ProcessAsync(Entry(envelope),ct);
            }
            else if (item.Kind == "WorkOrder")
            {
                if (item.FencingToken <= 0 || item.OperationId == Guid.Empty || item.RegistrationPayloadJson is null) { unresolved++; continue; }
                var payload = new WorkOrderLifecycleEvent(item.EquipmentId.Value,item.RegistrationId.Value,item.OperationId,
                    item.FencingToken,item.RegistrationPayloadJson,item.Status,item.Status is "InProgress" or "Completed");
                var envelope = new IntegrationEvent(eventId,WorkOrderLifecycleEvent.Updated,1,"Maintenance",item.ResourceId,
                    item.Version,page.CapturedAtUtc,message.CorrelationId,JsonSerializer.Serialize(payload));
                using var consumer = new WorkOrderLifecycleConsumer(config,scopes,Microsoft.Extensions.Logging.Abstractions.NullLogger<WorkOrderLifecycleConsumer>.Instance);
                await consumer.ProcessAsync(Entry(envelope),ct);
            }
            else await ConfirmScheduleAsync(item, message.CorrelationId, ct);
        }
        var now = DateTimeOffset.UtcNow;
        checkpoint.Advance(page.Index,page.PageHashes.Length,page.Items.Length,page.TotalItems,now,
            unresolved == 0 ? null : "Inventory contains unresolved operations or legacy records; retained as blockers.");
        db.Add(new InboxMessage(Group,message.EventId,message.EventType,1,now));
        db.Add(AuditEntry.ForService(message.Producer,"RegistrationSnapshotPageApplied","ReconciliationCheckpoint",page.SnapshotId,now,
            message.CorrelationId,JsonSerializer.Serialize(new { page.Index,page.TotalItems,unresolved,checkpoint.State,checkpoint.Cursor })));
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }

    private async Task ConfirmScheduleAsync(RegistrationSnapshotItem item, string correlation, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<EquipmentDbContext>();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var asset = await db.EquipmentAssets.FromSqlInterpolated($"SELECT * FROM dbo.EquipmentAssets WITH (UPDLOCK,HOLDLOCK) WHERE Id={item.EquipmentId}").SingleAsync(ct);
        var row = await db.EquipmentUseRegistrations.SingleAsync(x => x.Id == item.RegistrationId && x.EquipmentId == asset.Id &&
            x.SourceService == "Maintenance" && x.ResourceType == "MaintenanceSchedule" && x.ResourceId == item.ResourceId,ct);
        // Inactive/Ended without a completed release proof is still Active and still blocks retirement.
        if (row.ApplyScheduleVersion(item.Version,item.ScheduleReleaseConfirmed,DateTimeOffset.UtcNow))
            db.Add(AuditEntry.ForService("Maintenance","ScheduleRegistrationReconciled","EquipmentUseRegistration",row.Id,DateTimeOffset.UtcNow,
                correlation,JsonSerializer.Serialize(new { item.Version,row.State })));
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
    }
    private static StreamEntry Entry(IntegrationEvent message) => new("1-0",[new("event",JsonSerializer.Serialize(message))]);
}
