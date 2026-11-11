using System.Text.Json;
using MedEquipSLA.Contracts.Events;
using MedEquipSLA.Identity.Application.Abstractions;
using MedEquipSLA.Identity.Infrastructure;
using MedEquipSLA.Identity.Infrastructure.Persistence;
using MedEquipSLA.Notification.Infrastructure.Persistence;
using MedEquipSLA.Notification.Infrastructure.Realtime;
using MedEquipSLA.Notification.Infrastructure.Workers;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using Xunit;

namespace MedEquipSLA.Database.IntegrationTests;

public sealed class SessionRevocationBrokerTests
{
    [RedisSqlFact]
    public async Task Logout_stream_disconnects_only_target_session_and_all_logout_uses_new_authorization_version()
    {
        await using var redis = await ConnectionMultiplexer.ConnectAsync(Environment.GetEnvironmentVariable("REDIS_TEST_CONNECTION")!);
        RedisKey[] keys = [SessionRevokedEvent.Stream, UserAccessChangedEvent.Stream,
            SessionRevokedEvent.Stream + ":dead-letter", UserAccessChangedEvent.Stream + ":dead-letter"];
        foreach (var key in keys) Assert.False(await redis.GetDatabase().KeyExistsAsync(key), "Dedicated empty test streams required.");
        try
        {
            await using var sourceCatalog = await TestDatabase.CreateAsync("Identity");
            await using var targetCatalog = await TestDatabase.CreateAsync("Notification");
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
                { ["ConnectionStrings:Database"] = sourceCatalog.ConnectionString }).Build();
            await using var sourceServices = new ServiceCollection().AddLogging().AddSingleton<IConfiguration>(config)
                .AddInfrastructure(config).AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider()).BuildServiceProvider();
            await using var sourceScope = sourceServices.CreateAsyncScope();
            var source = sourceScope.ServiceProvider.GetRequiredService<IdentityDbContext>(); await source.Database.MigrateAsync();
            var users = sourceScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = ApplicationUser.CreateAdmin("tech", "tech@tests.invalid");
            Assert.True((await users.CreateAsync(user, "Valid!Session-Secret-234")).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, "Technician")).Succeeded);
            var sessions = sourceScope.ServiceProvider.GetRequiredService<IIdentitySessions>();
            var first = (await sessions.LoginAsync("tech", "Valid!Session-Secret-234", default))!;
            var second = (await sessions.LoginAsync("tech", "Valid!Session-Secret-234", default))!;
            var connections = new NotificationConnections(); var closedFirst = 0; var closedSecond = 0;
            Assert.True(connections.Register("first", user.Id, first.User.SessionId, 1, () => { closedFirst++; connections.Remove("first"); }));
            Assert.True(connections.Register("second", user.Id, second.User.SessionId, 1, () => { closedSecond++; connections.Remove("second"); }));
            await using var targetServices = new ServiceCollection().AddSingleton<IConnectionMultiplexer>(redis)
                .AddSingleton(connections).AddDbContext<NotificationDbContext>(o => o.UseSqlServer(targetCatalog.ConnectionString)).BuildServiceProvider();
            await using (var scope = targetServices.CreateAsyncScope()) await scope.ServiceProvider.GetRequiredService<NotificationDbContext>().Database.MigrateAsync();
            using var consumer = new NotificationConsumer(new ConfigurationBuilder().Build(), targetServices.GetRequiredService<IServiceScopeFactory>(), NullLogger<NotificationConsumer>.Instance);
            var publisher = new MedEquipSLA.Identity.Infrastructure.Messaging.RedisEventPublisher(redis);
            await sessions.RevokeAsync(first.User, false, default);
            Assert.False(await sessions.IsCurrentAsync(first.User, default)); Assert.True(await sessions.IsCurrentAsync(second.User, default));
            var queued = await source.OutboxMessages.AsNoTracking().SingleAsync();
            Assert.Equal(SessionRevokedEvent.Revoked, queued.EventType); Assert.Equal("User", queued.AggregateType);
            Assert.True(!queued.PayloadJson.Contains(first.RefreshToken, StringComparison.Ordinal), "Events must not contain refresh tokens.");
            await MedEquipSLA.Identity.Infrastructure.Workers.IdentityOutboxDispatcher.PublishAsync(source, publisher, default);
            await consumer.PollAsync(SessionRevokedEvent.Stream, "first-consumer", "0-0", default);
            Assert.Equal(1, closedFirst); Assert.Equal(0, closedSecond);
            var duplicate = new IntegrationEvent(queued.EventId, queued.EventType, 1, "Identity", queued.AggregateId,
                queued.AggregateVersion, queued.OccurredAtUtc, queued.CorrelationId, queued.PayloadJson);
            await publisher.PublishAsync(SessionRevokedEvent.Stream, duplicate, default);
            await consumer.PollAsync(SessionRevokedEvent.Stream, "restarted", "0-0", default);
            Assert.Equal(1, closedFirst); Assert.Equal(0, closedSecond);
            Assert.False(connections.Register("late", user.Id, first.User.SessionId, 1, () => { }));
            await sessions.RevokeAsync(second.User, true, default);
            await MedEquipSLA.Identity.Infrastructure.Workers.IdentityOutboxDispatcher.PublishAsync(source, publisher, default);
            await consumer.PollAsync(UserAccessChangedEvent.Stream, "all-logout", "0-0", default);
            Assert.Equal(1, closedSecond);
            Assert.False(await sessions.IsCurrentAsync(second.User, default));
            await using var targetScope = targetServices.CreateAsyncScope();
            Assert.Equal(2, await targetScope.ServiceProvider.GetRequiredService<NotificationDbContext>().InboxMessages.CountAsync());
            Assert.Equal(0, (await redis.GetDatabase().StreamPendingAsync(SessionRevokedEvent.Stream, NotificationConsumer.SessionGroup)).PendingMessageCount);
            Assert.Equal(0, (await redis.GetDatabase().StreamPendingAsync(UserAccessChangedEvent.Stream, NotificationConsumer.UserAccessGroup)).PendingMessageCount);
        }
        finally { await redis.GetDatabase().KeyDeleteAsync(keys); }
    }
}
