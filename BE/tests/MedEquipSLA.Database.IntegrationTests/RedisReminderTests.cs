using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using MedEquipSLA.Contracts.Events;
using MedEquipSLA.Maintenance.Infrastructure.Persistence;
using MedEquipSLA.Maintenance.Infrastructure.Workers;
using MedEquipSLA.Notification.Infrastructure.Persistence;
using MedEquipSLA.Notification.Infrastructure.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using Xunit;
using RedisPublisher = MedEquipSLA.Maintenance.Infrastructure.Messaging.RedisEventPublisher;

namespace MedEquipSLA.Database.IntegrationTests;

public sealed class RedisSqlFactAttribute : FactAttribute
{
    public RedisSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SQLSERVER_TEST_CONNECTION")) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("REDIS_TEST_CONNECTION")))
            Skip = "BLOCKED: real SQL Server and dedicated REDIS_TEST_CONNECTION are required.";
    }
}

public sealed class RedisReminderTests
{
    [RedisSqlFact]
    public async Task User_notification_outbox_routes_real_stream_and_inbox_prevents_duplicate_after_restart()
    {
        await using var redis = await RedisFixture.CreateAsync();
        await using var sourceDatabase = await TestDatabase.CreateAsync("Maintenance");
        await using var targetDatabase = await TestDatabase.CreateAsync("Notification");
        await using var source = (MaintenanceDbContext)Contexts.Create("Maintenance", sourceDatabase.ConnectionString);
        await source.Database.MigrateAsync();
        var technician = Guid.NewGuid();
        var workOrder = Guid.NewGuid();
        var envelope = new UserNotificationEvent("WorkOrderAssigned", "Recipient", technician, "WorkOrder", workOrder,
            null, "Work assigned", "Open your assigned WorkOrder.").Envelope("Maintenance", 1, DateTimeOffset.UtcNow, "redis-user-test");
        source.OutboxMessages.Add(new MedEquipSLA.Maintenance.Infrastructure.Persistence.Entities.OutboxMessage(envelope));
        await source.SaveChangesAsync();
        await using var services = Services(redis.Connection, targetDatabase.ConnectionString, new FixedRecipients());
        await using (var scope = services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<NotificationDbContext>().Database.MigrateAsync();
        using (var publisher = Dispatcher(services))
            await publisher.PublishBatchAsync(source, new RedisPublisher(redis.Connection), default);
        Assert.Equal("Published", (await source.OutboxMessages.AsNoTracking().SingleAsync()).Status);
        using (var first = Consumer(services))
            await first.PollAsync(UserNotificationEvent.Stream, "first", "0-0", default);
        await new RedisPublisher(redis.Connection).PublishAsync(UserNotificationEvent.Stream, envelope, default);
        using (var restarted = Consumer(services))
            await restarted.PollAsync(UserNotificationEvent.Stream, "restarted", "0-0", default);
        await AssertStoredAsync(services, 1);
        await using (var scope = services.CreateAsyncScope())
        {
            var notification = await scope.ServiceProvider.GetRequiredService<NotificationDbContext>().Notifications.SingleAsync();
            Assert.Equal(technician, notification.RecipientUserId);
            Assert.Equal(workOrder, notification.ResourceId);
        }
        Assert.Equal(0, (await redis.Db.StreamPendingAsync(UserNotificationEvent.Stream, NotificationConsumer.UserGroup)).PendingMessageCount);
    }
    [RedisSqlFact]
    public async Task Real_outbox_stream_consumer_ack_and_duplicate_notification()
    {
        await using var redis = await RedisFixture.CreateAsync();
        await using var sourceDb = await TestDatabase.CreateAsync("Maintenance");
        await using var targetDb = await TestDatabase.CreateAsync("Notification");
        await using var source = (MaintenanceDbContext)Contexts.Create("Maintenance", sourceDb.ConnectionString);
        await source.Database.MigrateAsync();
        await MaintenanceReminderTests.SeedAsync(source);
        var lookup = new MaintenanceReminderTests.RecipientStub();
        var planner = new ReminderPlanner(source, lookup, NullLogger<ReminderPlanner>.Instance);
        Assert.Equal(1, await planner.ScanAsync(MaintenanceReminderTests.Scheduled.AddMinutes(-30), default));
        var outbox = await source.OutboxMessages.AsNoTracking().SingleAsync();

        await using var services = Services(redis.Connection, targetDb.ConnectionString, lookup);
        await using (var scope = services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<NotificationDbContext>().Database.MigrateAsync();
        using var publisher = Dispatcher(services);
        await publisher.PublishBatchAsync(source, new RedisPublisher(redis.Connection), default);
        Assert.Equal("Published", (await source.OutboxMessages.AsNoTracking().SingleAsync()).Status);
        using var consumer = Consumer(services);
        await consumer.PollAsync(MaintenanceReminderEvent.Stream, "first", "0-0", default);
        await AssertStoredAsync(services, 1);
        Assert.Equal(0, (await redis.Db.StreamPendingAsync(MaintenanceReminderEvent.Stream, NotificationConsumer.ReminderGroup)).PendingMessageCount);

        var duplicate = new IntegrationEvent(outbox.EventId, outbox.EventType, 1, "Maintenance", outbox.AggregateId,
            outbox.AggregateVersion, outbox.OccurredAtUtc, outbox.CorrelationId, outbox.PayloadJson);
        await new RedisPublisher(redis.Connection).PublishAsync(MaintenanceReminderEvent.Stream, duplicate, default);
        await consumer.PollAsync(MaintenanceReminderEvent.Stream, "first", "0-0", default);
        await AssertStoredAsync(services, 1);
    }

    [RedisSqlFact]
    public async Task Sql_failure_is_not_acked_restart_reclaims_pending_and_poison_is_dead_lettered()
    {
        await using var redis = await RedisFixture.CreateAsync();
        await using var database = await TestDatabase.CreateAsync("Notification");
        var admin = Guid.NewGuid();
        var (message, payload) = MaintenanceReminderInboxTests.Event([admin]);
        await using var services = Services(redis.Connection, database.ConnectionString, new FixedRecipients(admin));
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
            await db.Database.MigrateAsync();
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.InboxMessages ADD CONSTRAINT TestInboxRedisFailure CHECK (EventType <> 'MaintenanceDueSoon')");
        }

        await new RedisPublisher(redis.Connection).PublishAsync(MaintenanceReminderEvent.Stream, message, default);
        using (var first = Consumer(services))
            await first.PollAsync(MaintenanceReminderEvent.Stream, "before-restart", "0-0", default);
        Assert.Equal(1, (await redis.Db.StreamPendingAsync(MaintenanceReminderEvent.Stream, NotificationConsumer.ReminderGroup)).PendingMessageCount);
        await AssertStoredAsync(services, 0);
        await using (var scope = services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<NotificationDbContext>().Database
                .ExecuteSqlRawAsync("ALTER TABLE dbo.InboxMessages DROP CONSTRAINT TestInboxRedisFailure");

        await Task.Delay(200);
        using (var restarted = Consumer(services))
            await restarted.PollAsync(MaintenanceReminderEvent.Stream, "after-restart", "0-0", default);
        await AssertStoredAsync(services, 1);
        Assert.Equal(0, (await redis.Db.StreamPendingAsync(MaintenanceReminderEvent.Stream, NotificationConsumer.ReminderGroup)).PendingMessageCount);

        await redis.Db.StreamAddAsync(MaintenanceReminderEvent.Stream, [new("event", "{bad json")]);
        using (var consumer = Consumer(services))
            await consumer.PollAsync(MaintenanceReminderEvent.Stream, "poison-reader", "0-0", default);
        Assert.Equal(1, await redis.Db.StreamLengthAsync(MaintenanceReminderEvent.Stream + ":dead-letter"));
        Assert.Equal(0, (await redis.Db.StreamPendingAsync(MaintenanceReminderEvent.Stream, NotificationConsumer.ReminderGroup)).PendingMessageCount);
    }

    [RedisSqlFact]
    public async Task Restart_after_sql_commit_before_ack_does_not_duplicate_and_sla_still_works()
    {
        await using var redis = await RedisFixture.CreateAsync();
        await using var database = await TestDatabase.CreateAsync("Notification");
        var admin = Guid.NewGuid();
        var (message, payload) = MaintenanceReminderInboxTests.Event([admin]);
        await using var services = Services(redis.Connection, database.ConnectionString, new FixedRecipients(admin));
        await using (var scope = services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<NotificationDbContext>().Database.MigrateAsync();

        await redis.Db.StreamCreateConsumerGroupAsync(MaintenanceReminderEvent.Stream, NotificationConsumer.ReminderGroup,
            "0-0", createStream: true);
        await new RedisPublisher(redis.Connection).PublishAsync(MaintenanceReminderEvent.Stream, message, default);
        Assert.Single(await redis.Db.StreamReadGroupAsync(MaintenanceReminderEvent.Stream,
            NotificationConsumer.ReminderGroup, "lost-consumer", ">", 10));
        await using (var scope = services.CreateAsyncScope())
            await NotificationConsumer.StoreReminderAsync(scope.ServiceProvider.GetRequiredService<NotificationDbContext>(),
                message, payload, [admin], DateTimeOffset.UtcNow, default);
        // Simulate the process boundary after SQL commit and before XACK.
        await Task.Delay(200);
        using var restarted = Consumer(services);
        await restarted.PollAsync(MaintenanceReminderEvent.Stream, "new-consumer", "0-0", default);
        await AssertStoredAsync(services, 1);
        Assert.Equal(0, (await redis.Db.StreamPendingAsync(MaintenanceReminderEvent.Stream, NotificationConsumer.ReminderGroup)).PendingMessageCount);

        var slaPayload = new SlaAlertEvent(admin, Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-1));
        var sla = new IntegrationEvent(Guid.NewGuid(), SlaAlertEvent.ResponseBreached, 1, "Incident", Guid.NewGuid(), 1,
            DateTimeOffset.UtcNow, "redis-sla-test", JsonSerializer.Serialize(slaPayload));
        await new RedisPublisher(redis.Connection).PublishAsync(SlaAlertEvent.Stream, sla, default);
        await restarted.PollAsync(SlaAlertEvent.Stream, "sla-regression", "0-0", default);
        await AssertStoredAsync(services, 2);
        Assert.Equal(0, (await redis.Db.StreamPendingAsync(SlaAlertEvent.Stream, "notification-sla-v1")).PendingMessageCount);
    }

    [RedisSqlFact]
    public async Task Real_connection_loss_keeps_outbox_then_reconnect_publishes_same_event()
    {
        await using var redis = await RedisFixture.CreateAsync();
        await using var database = await TestDatabase.CreateAsync("Maintenance");
        await using var db = (MaintenanceDbContext)Contexts.Create("Maintenance", database.ConnectionString);
        await db.Database.MigrateAsync();
        await MaintenanceReminderTests.SeedAsync(db);
        await new ReminderPlanner(db, new FixedRecipients(Guid.NewGuid()), NullLogger<ReminderPlanner>.Instance)
            .ScanAsync(MaintenanceReminderTests.Scheduled.AddMinutes(-30), default);
        var eventId = (await db.OutboxMessages.AsNoTracking().SingleAsync()).EventId;
        var options = ConfigurationOptions.Parse(Environment.GetEnvironmentVariable("REDIS_TEST_CONNECTION")!);
        var target = options.EndPoints.Single();
        await using var proxy = new RedisProxy(target);
        options.EndPoints.Clear();
        options.EndPoints.Add(IPAddress.Loopback, proxy.Port);
        options.AbortOnConnectFail = false;
        options.ConnectTimeout = 500;
        options.AsyncTimeout = 500;
        options.ConnectRetry = 0;
        await using var connection = await ConnectionMultiplexer.ConnectAsync(options);
        await connection.GetDatabase().PingAsync();
        proxy.Disconnect();

        await using var services = new ServiceCollection().BuildServiceProvider();
        using var dispatcher = Dispatcher(services);
        var publisher = new RedisPublisher(connection);
        await dispatcher.PublishBatchAsync(db, publisher, default);
        Assert.Equal("Pending", (await db.OutboxMessages.AsNoTracking().SingleAsync()).Status);
        Assert.Empty(await redis.Db.StreamRangeAsync(MaintenanceReminderEvent.Stream));
        proxy.Recover();
        for (var attempt = 0; attempt < 30; attempt++)
        {
            await db.OutboxMessages.ExecuteUpdateAsync(update => update
                .SetProperty(x => x.NextAttemptAtUtc, DateTimeOffset.UtcNow.AddMinutes(-1)));
            await dispatcher.PublishBatchAsync(db, publisher, default);
            if ((await db.OutboxMessages.AsNoTracking().SingleAsync()).Status == "Published") break;
            await Task.Delay(200);
        }
        Assert.Equal("Published", (await db.OutboxMessages.AsNoTracking().SingleAsync()).Status);
        var entry = Assert.Single(await redis.Db.StreamRangeAsync(MaintenanceReminderEvent.Stream));
        var envelope = JsonSerializer.Deserialize<IntegrationEvent>(entry.Values.Single(x => x.Name == "event").Value.ToString())!;
        Assert.Equal(eventId, envelope.EventId);
    }

    private static ServiceProvider Services(IConnectionMultiplexer redis, string connection, INotificationRecipientLookup lookup) =>
        new ServiceCollection().AddSingleton(redis).AddSingleton(lookup).AddSingleton(TimeProvider.System)
            .AddDbContext<NotificationDbContext>(options => options.UseSqlServer(connection)).BuildServiceProvider();

    private static NotificationConsumer Consumer(ServiceProvider services) => new(
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["Workers:ConsumerClaimIdleMilliseconds"] = "100" }).Build(),
        services.GetRequiredService<IServiceScopeFactory>(), NullLogger<NotificationConsumer>.Instance);

    private static ScheduleOutboxDispatcher Dispatcher(ServiceProvider services) =>
        new(new ConfigurationBuilder().Build(), services.GetRequiredService<IServiceScopeFactory>(),
            TimeProvider.System, NullLogger<ScheduleOutboxDispatcher>.Instance);

    private static async Task AssertStoredAsync(ServiceProvider services, int count)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        Assert.Equal(count, await db.Notifications.CountAsync());
        Assert.Equal(count, await db.InboxMessages.CountAsync());
    }

    private sealed class FixedRecipients(params Guid[] users) : INotificationRecipientLookup
    {
        public Task<Guid[]> ResolveAsync(Guid? technicianId, CancellationToken ct) => Task.FromResult(users);
    }

    private sealed class RedisFixture(ConnectionMultiplexer connection) : IAsyncDisposable
    {
        public IConnectionMultiplexer Connection => connection;
        public IDatabase Db => connection.GetDatabase();
        private static readonly RedisKey[] Keys = [MaintenanceReminderEvent.Stream,
            MaintenanceReminderEvent.Stream + ":dead-letter", SlaAlertEvent.Stream, SlaAlertEvent.Stream + ":dead-letter",
            UserNotificationEvent.Stream, UserNotificationEvent.Stream + ":dead-letter"];

        public static async Task<RedisFixture> CreateAsync()
        {
            var connection = await ConnectionMultiplexer.ConnectAsync(Environment.GetEnvironmentVariable("REDIS_TEST_CONNECTION")!);
            foreach (var key in Keys)
                if (await connection.GetDatabase().KeyExistsAsync(key))
                {
                    await connection.DisposeAsync();
                    throw new InvalidOperationException("Use a dedicated empty Redis database for integration tests.");
                }
            return new(connection);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.KeyDeleteAsync(Keys);
            await connection.DisposeAsync();
        }
    }

    private sealed class RedisProxy : IAsyncDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource stop = new();
        private readonly List<TcpClient> clients = [];
        private readonly Task accepting;
        private readonly EndPoint target;
        private volatile bool available = true;
        public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;

        public RedisProxy(EndPoint target)
        {
            this.target = target;
            listener.Start();
            accepting = AcceptAsync();
        }

        public void Disconnect()
        {
            available = false;
            lock (clients) { foreach (var client in clients) client.Dispose(); clients.Clear(); }
        }

        public void Recover() => available = true;

        private async Task AcceptAsync()
        {
            try
            {
                while (!stop.IsCancellationRequested)
                {
                    var client = await listener.AcceptTcpClientAsync(stop.Token);
                    _ = ForwardAsync(client);
                }
            }
            catch (OperationCanceledException) { }
        }

        private async Task ForwardAsync(TcpClient client)
        {
            using (client)
            using (var upstream = new TcpClient())
            {
                try
                {
                    if (!available) return;
                    lock (clients) { clients.Add(client); clients.Add(upstream); }
                    switch (target)
                    {
                        case DnsEndPoint dns: await upstream.ConnectAsync(dns.Host, dns.Port, stop.Token); break;
                        case IPEndPoint ip: await upstream.ConnectAsync(ip.Address, ip.Port, stop.Token); break;
                        default: throw new InvalidOperationException("Unsupported Redis endpoint.");
                    }
                    using var copy = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
                    var down = client.GetStream().CopyToAsync(upstream.GetStream(), copy.Token);
                    var up = upstream.GetStream().CopyToAsync(client.GetStream(), copy.Token);
                    await Task.WhenAny(down, up);
                    await copy.CancelAsync();
                    await Task.WhenAll(down, up);
                }
                catch (Exception e) when (e is IOException or SocketException or OperationCanceledException or ObjectDisposedException) { }
                finally { lock (clients) { clients.Remove(client); clients.Remove(upstream); } }
            }
        }

        public async ValueTask DisposeAsync()
        {
            await stop.CancelAsync();
            listener.Stop();
            Disconnect();
            await accepting;
            stop.Dispose();
        }
    }
}
