using MedEquipSLA.Contracts.Events;
using MedEquipSLA.Equipment.Infrastructure.Persistence;
using MedEquipSLA.Equipment.Infrastructure.Persistence.Entities;
using MedEquipSLA.Equipment.Infrastructure.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MedEquipSLA.Database.IntegrationTests;

public sealed class EquipmentOutboxTests
{
    [SqlServerFact]
    public async Task Failed_publish_is_retried_with_the_same_event_id()
    {
        await using var database = await TestDatabase.CreateAsync("Equipment");
        await using var db = (EquipmentDbContext)Contexts.Create("Equipment", database.ConnectionString);
        await db.Database.MigrateAsync();
        var id = Guid.NewGuid();
        db.OutboxMessages.Add(new OutboxMessage(new IntegrationEvent(id, EquipmentDepartmentChanged.EventType,
            1, "Equipment", Guid.NewGuid(), 2, DateTimeOffset.UtcNow, "integration-test", "{}")));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        await using var services = new ServiceCollection().BuildServiceProvider();
        using var dispatcher = new EquipmentOutboxDispatcher(new ConfigurationBuilder().Build(),
            services.GetRequiredService<IServiceScopeFactory>(), NullLogger<EquipmentOutboxDispatcher>.Instance);
        var publisher = new RecordingPublisher();
        await dispatcher.PublishBatchAsync(db, publisher, default);
        var pending = await db.OutboxMessages.AsNoTracking().SingleAsync();
        Assert.Equal("Pending", pending.Status);
        Assert.NotNull(pending.NextAttemptAtUtc);
        Assert.Null(pending.LeaseOwner);

        await db.OutboxMessages.ExecuteUpdateAsync(update => update.SetProperty(x => x.NextAttemptAtUtc, DateTimeOffset.UtcNow.AddMinutes(-1)));
        await dispatcher.PublishBatchAsync(db, publisher, default);
        await dispatcher.PublishBatchAsync(db, publisher, default);
        var published = await db.OutboxMessages.AsNoTracking().SingleAsync();
        Assert.Equal("Published", published.Status);
        Assert.Equal(2, published.AttemptCount);
        Assert.Equal(new[] { id, id }, publisher.EventIds);
    }

    private sealed class RecordingPublisher : IEventPublisher
    {
        public List<Guid> EventIds { get; } = [];

        public Task PublishAsync(string stream, IntegrationEvent message, CancellationToken cancellationToken)
        {
            Assert.Equal(EquipmentDepartmentChanged.Stream, stream);
            Assert.Equal("Equipment", message.Producer);
            EventIds.Add(message.EventId);
            return EventIds.Count == 1 ? Task.FromException(new IOException("Test broker outage")) : Task.CompletedTask;
        }
    }
}
