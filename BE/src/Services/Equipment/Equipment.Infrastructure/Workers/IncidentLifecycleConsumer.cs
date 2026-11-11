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

public sealed class IncidentLifecycleConsumer(IConfiguration config, IServiceScopeFactory scopes,
    ILogger<IncidentLifecycleConsumer> logger) : BackgroundService
{
    private const string Stream = IncidentLifecycleEvent.Stream;
    private const string Group = "equipment-incident-v1";

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
                        logger.LogWarning(e, "Incident lifecycle event {EntryId} remains pending.", entry.Id);
                    }
                }
            }
            catch (Exception e) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning(e, "Incident lifecycle stream unavailable.");
            }
        } while (await timer.WaitForNextTickAsync(ct));
    }

    public async Task ProcessAsync(StreamEntry entry, CancellationToken ct)
    {
        var raw = entry.Values.FirstOrDefault(value => value.Name == "event").Value;
        var message = JsonSerializer.Deserialize<IntegrationEvent>(raw.ToString());
        if (message is null || message.Producer != "Incident" || message.SchemaVersion != 1 ||
            message.EventId == Guid.Empty || message.AggregateId == Guid.Empty || message.AggregateVersion < 1 ||
            message.EventType is not (IncidentLifecycleEvent.Created or IncidentLifecycleEvent.Closed))
            throw new JsonException("Invalid incident lifecycle envelope.");
        var payload = JsonSerializer.Deserialize<IncidentLifecycleEvent>(message.Payload);
        var closed = message.EventType == IncidentLifecycleEvent.Closed;
        if (payload is null || payload.EquipmentId == Guid.Empty || payload.RegistrationId == Guid.Empty ||
            payload.Status != (closed ? "Closed" : "Open"))
            throw new JsonException("Invalid incident lifecycle payload.");

        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<EquipmentDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var asset = await db.EquipmentAssets.FromSqlInterpolated(
            $"SELECT * FROM dbo.EquipmentAssets WITH (UPDLOCK, HOLDLOCK) WHERE Id = {payload.EquipmentId}").SingleAsync(ct);
        if (await db.InboxMessages.AnyAsync(x => x.ConsumerName == Group && x.EventId == message.EventId, ct))
            return;
        var registration = await db.EquipmentUseRegistrations.SingleAsync(x => x.Id == payload.RegistrationId &&
            x.EquipmentId == asset.Id && x.SourceService == "Incident" && x.ResourceType == "Incident" &&
            x.ResourceId == message.AggregateId, ct);
        var now = DateTimeOffset.UtcNow;
        if (registration.ApplyIncidentVersion(message.AggregateVersion, closed, now))
        {
            var projection = await db.EquipmentStateProjections.SingleOrDefaultAsync(x =>
                x.SourceService == "Incident" && x.SourceAggregateId == message.AggregateId, ct);
            if (projection is null)
            {
                projection = EquipmentStateProjection.ForIncident(message.AggregateId, asset.Id);
                db.EquipmentStateProjections.Add(projection);
            }
            projection.ApplyIncident(payload.Status, message.AggregateVersion, message.EventId, message.OccurredAtUtc, now);
            await db.SaveChangesAsync(ct);
            var blocking = db.EquipmentStateProjections.Where(x => x.EquipmentId == asset.Id && x.IsBlocking);
            var incident = await blocking.AnyAsync(x => x.SourceService == "Incident", ct);
            var maintenance = await blocking.AnyAsync(x => x.SourceService == "Maintenance", ct);
            var previous = asset.OperationalStatus;
            if (asset.ApplyBlockingStatus(incident, maintenance, now))
                db.EquipmentStatusHistories.Add(EquipmentStatusHistory.Change(asset.Id, previous, asset.OperationalStatus, message.EventId, now));
        }
        db.InboxMessages.Add(new InboxMessage(Group, message.EventId, message.EventType, message.SchemaVersion, now));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
