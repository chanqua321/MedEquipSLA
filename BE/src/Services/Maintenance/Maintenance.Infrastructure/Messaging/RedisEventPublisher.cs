using System.Text.Json;
using MedEquipSLA.Contracts.Events;
using StackExchange.Redis;
namespace MedEquipSLA.Maintenance.Infrastructure.Messaging;
public sealed class RedisEventPublisher(IConnectionMultiplexer redis) : IEventPublisher
{
    public async Task PublishAsync(string stream, IntegrationEvent message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await redis.GetDatabase().StreamAddAsync(stream, [new NameValueEntry("event", JsonSerializer.Serialize(message))]);
    }
}
