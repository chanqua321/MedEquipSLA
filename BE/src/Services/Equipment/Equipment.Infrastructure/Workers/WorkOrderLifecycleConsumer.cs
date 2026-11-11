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

public sealed class WorkOrderLifecycleConsumer(IConfiguration config, IServiceScopeFactory scopes,
    ILogger<WorkOrderLifecycleConsumer> logger) : BackgroundService
{
    private const string Stream = WorkOrderLifecycleEvent.Stream;
    private const string Group = "equipment-workorder-v1";

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!config.GetValue<bool>("Workers:Enabled")) return;
        var consumer = Guid.NewGuid().ToString("N");
        RedisValue cursor = "0-0";
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var redis = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>().GetDatabase();
                try { await redis.StreamCreateConsumerGroupAsync(Stream, Group, "0-0", createStream: true); }
                catch (RedisServerException e) when (e.Message.StartsWith("BUSYGROUP", StringComparison.Ordinal)) { }
                var retry = await redis.StreamAutoClaimAsync(Stream, Group, consumer, 30000, cursor, count: 10);
                cursor = retry.NextStartId;
                var fresh = await redis.StreamReadGroupAsync(Stream, Group, consumer, ">", count: 10);
                foreach (var entry in retry.ClaimedEntries.Concat(fresh))
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        await ProcessAsync(entry, ct);
                        await redis.StreamAcknowledgeAsync(Stream, Group, entry.Id);
                    }
                    catch (JsonException)
                    {
                        await redis.StreamAddAsync(Stream + ":dead-letter", entry.Values);
                        await redis.StreamAcknowledgeAsync(Stream, Group, entry.Id);
                    }
                    catch (Exception e) when (!ct.IsCancellationRequested)
                    {
                        logger.LogWarning(e, "WorkOrder lifecycle event {EntryId} remains pending.", entry.Id);
                    }
                }
            }
            catch (Exception e) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning(e, "WorkOrder lifecycle stream unavailable.");
            }
        } while (await timer.WaitForNextTickAsync(ct));
    }


    public async Task ProcessAsync(StreamEntry entry, CancellationToken ct)
    {
        var raw = entry.Values.FirstOrDefault(value => value.Name == "event").Value;
        var message = JsonSerializer.Deserialize<IntegrationEvent>(raw.ToString());
        if (message is null || message.Producer != "Maintenance" || message.SchemaVersion != 1 ||
            message.EventId == Guid.Empty || message.AggregateId == Guid.Empty || message.AggregateVersion < 1 ||
            message.EventType is not (WorkOrderLifecycleEvent.Created or WorkOrderLifecycleEvent.Updated))
            throw new JsonException("Invalid WorkOrder envelope.");
        var payload = JsonSerializer.Deserialize<WorkOrderLifecycleEvent>(message.Payload);
        if (payload is null || payload.EquipmentId == Guid.Empty || payload.RegistrationId == Guid.Empty ||
            payload.OperationId == Guid.Empty || payload.FencingToken <= 0 ||
            payload.Status is not ("Scheduled" or "InProgress" or "Completed" or "Closed" or "Cancelled") ||
            payload.IsBlocking != (payload.Status is "InProgress" or "Completed"))
            throw new JsonException("Invalid WorkOrder state.");
        WorkOrderRegistrationRequest request;
        try
        {
            using var json = JsonDocument.Parse(payload.RegistrationPayloadJson);
            var r = json.RootElement;
            if (r.GetProperty("version").GetInt32() != 1) throw new JsonException("Unknown registration version.");
            request = new(r.GetProperty("equipmentId").GetGuid(), r.GetProperty("scheduleId").GetGuid(),
                r.GetProperty("occurrenceId").GetGuid(), r.GetProperty("workOrderId").GetGuid(),
                r.GetProperty("workType").GetString()!, r.GetProperty("scheduledForUtc").GetDateTimeOffset(),
                r.GetProperty("dueAtUtc").GetDateTimeOffset());
            if (request.CanonicalJson() != payload.RegistrationPayloadJson || request.EquipmentId != payload.EquipmentId ||
                request.WorkOrderId != message.AggregateId) throw new JsonException("Registration binding mismatch.");
        }
        catch (Exception error) when (error is InvalidOperationException or KeyNotFoundException or FormatException)
        { throw new JsonException("Malformed WorkOrder registration.", error); }

        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<EquipmentDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var asset = await db.EquipmentAssets.FromSqlInterpolated(
            $"SELECT * FROM dbo.EquipmentAssets WITH (UPDLOCK,HOLDLOCK) WHERE Id={payload.EquipmentId}").SingleAsync(ct);
        if (await db.InboxMessages.AnyAsync(x => x.ConsumerName == Group && x.EventId == message.EventId, ct)) return;
        var registration = await db.EquipmentUseRegistrations.SingleAsync(x => x.Id == payload.RegistrationId &&
            x.EquipmentId == asset.Id && x.SourceService == "Maintenance" && x.SourceOperationId == payload.OperationId, ct);
        if (!registration.MatchesWorkOrder(request, payload.OperationId))
            throw new InvalidOperationException("Registration hash/binding mismatch.");
        var now = DateTimeOffset.UtcNow;
        var applied = registration.ApplyWorkOrderVersion(request, payload.OperationId, payload.FencingToken,
            message.AggregateVersion, payload.Status, now);
        var projection = await db.EquipmentStateProjections.SingleOrDefaultAsync(x =>
            x.SourceService == "Maintenance" && x.SourceAggregateId == message.AggregateId, ct);
        // Confirm RPC may have already applied SourceVersion. Its matching Created event may still build the projection.
        if (applied || registration.State == "Active" && registration.SourceVersion == message.AggregateVersion &&
            projection is null)
        {
            if (projection is null)
            { projection = EquipmentStateProjection.ForWorkOrder(message.AggregateId, asset.Id); db.Add(projection); }
            projection.ApplyWorkOrder(payload.Status, message.AggregateVersion, message.EventId, message.OccurredAtUtc, now);
            await db.SaveChangesAsync(ct);
            var blocking = db.EquipmentStateProjections.Where(x => x.EquipmentId == asset.Id && x.IsBlocking);
            // WorkOrder events cannot prove an existing Broken status has been repaired.
            // Incident Closed is the owner transition which can clear that condition.
            var broken = asset.OperationalStatus == "Broken" || await blocking.AnyAsync(x => x.SourceService == "Incident", ct);
            var maintenance = await blocking.AnyAsync(x => x.SourceService == "Maintenance", ct);
            var previous = asset.OperationalStatus;
            if (asset.ApplyBlockingStatus(broken, maintenance, now))
                db.Add(EquipmentStatusHistory.Change(asset.Id, previous, asset.OperationalStatus, message.EventId, now));
            db.AuditEntries.Add(AuditEntry.ForService("Maintenance", "WorkOrderProjectionApplied", "EquipmentUseRegistration",
                registration.Id, now, message.CorrelationId, JsonSerializer.Serialize(new { message.EventId, message.AggregateVersion, payload.Status })));
        }
        db.Add(new InboxMessage(Group, message.EventId, message.EventType, message.SchemaVersion, now));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
