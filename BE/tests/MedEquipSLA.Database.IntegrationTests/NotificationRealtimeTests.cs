using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using MedEquipSLA.Contracts.Events;
using MedEquipSLA.Hosting;
using MedEquipSLA.Notification.Domain;
using MedEquipSLA.Notification.Infrastructure.Persistence;
using MedEquipSLA.Notification.Infrastructure.Realtime;
using MedEquipSLA.Notification.Infrastructure.Workers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using static MedEquipSLA.Database.IntegrationTests.EquipmentWorkOrderGrpcTests;

namespace MedEquipSLA.Database.IntegrationTests;

public sealed class NotificationRealtimeTests
{
    private sealed class SessionDirectory : HttpMessageHandler
    {
        internal HashSet<string> ActiveTokens { get; } = [];
        internal bool Available = true;
        internal string? LastToken;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastToken = request.Headers.Authorization?.Parameter;
            return Task.FromResult(new HttpResponseMessage(!Available ? HttpStatusCode.ServiceUnavailable :
                request.Headers.Authorization?.Scheme == "Bearer" && LastToken is not null && ActiveTokens.Contains(LastToken)
                    ? HttpStatusCode.OK : HttpStatusCode.Unauthorized));
        }
    }
    [SqlServerFact]
    public async Task Authenticated_websockets_receive_only_own_durable_notification_hints_and_rest_stays_authoritative()
    {
        await using var issuer = await ProtocolHost.CreateAsync();
        await using var catalog = await TestDatabase.CreateAsync("Notification");
        await using (var db = Contexts.Create("Notification", catalog.ConnectionString)) await db.Database.MigrateAsync();
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?>
        {
            ["Workers:Enabled"] = "true", ["Jwt:PublicKeyPath"] = issuer.VerificationKeyFile,
            ["Jwt:Issuer"] = "test-issuer", ["Jwt:Audience"] = "test-audience"
        });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Services.AddServiceScaffold(builder.Configuration);
        var directory = new SessionDirectory();
        builder.Services.AddIdentitySessionValidation(builder.Configuration);
        builder.Services.AddHttpClient("IdentitySession").ConfigurePrimaryHttpMessageHandler(() => directory);
        builder.Services.AddHubQueryTokenSupport("/hubs/notifications");
        builder.Services.AddDbContext<NotificationDbContext>(o => o.UseSqlServer(catalog.ConnectionString));
        builder.Services.AddSignalR();
        builder.Services.AddSingleton<NotificationConnections>();
        builder.Services.AddSingleton<IUserIdProvider, NotificationUserIdProvider>();
        builder.Services.AddHostedService<NotificationHintWorker>();
        await using var app = builder.Build();
        app.UseAuthentication(); app.UseAuthorization();
        app.MapHub<NotificationHub>("/hubs/notifications").RequireAuthorization();
        app.MapGet("/guard", () => "protected").RequireAuthorization();
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        using var http = new HttpClient { BaseAddress = new Uri(address) };
        using var anonymous = await http.PostAsync("/hubs/notifications/negotiate?negotiateVersion=1", null);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        using var queryOnRest = await http.GetAsync("/guard?access_token=" + issuer.AdminToken("Technician", a));
        Assert.Equal(HttpStatusCode.Unauthorized, queryOnRest.StatusCode);
        var tokenA = issuer.AdminToken("Technician", a); var tokenB = issuer.AdminToken("Technician", b);
        directory.ActiveTokens.UnionWith([tokenA, tokenB]);
        using var socketA = await ConnectAsync(address, tokenA);
        using var socketB = await ConnectAsync(address, tokenB);
        var revoked = issuer.AdminToken("Technician", Guid.NewGuid());
        using (var mixed = new ClientWebSocket())
        {
            mixed.Options.SetRequestHeader("Authorization", "Bearer " + tokenA);
            await Assert.ThrowsAsync<WebSocketException>(() => mixed.ConnectAsync(new Uri(address.Replace("http://", "ws://", StringComparison.Ordinal) +
                "/hubs/notifications?access_token=" + Uri.EscapeDataString(revoked)), CancellationToken.None));
            Assert.True(directory.LastToken == revoked, "Freshness must use the validated query token, not another user's header.");
        }
        directory.Available = false;
        http.DefaultRequestHeaders.Authorization = new("Bearer", tokenA);
        using (var offline = await http.GetAsync("/guard")) Assert.Equal(HttpStatusCode.Unauthorized, offline.StatusCode);
        directory.Available = true;
        var noticeA = NotificationRecord.UserEvent(a, Guid.NewGuid(), "WorkOrderAssigned", "WorkOrder", Guid.NewGuid(), null,
            "Assigned A", "A", DateTimeOffset.UtcNow);
        var noticeB = NotificationRecord.UserEvent(b, Guid.NewGuid(), "WorkOrderAssigned", "WorkOrder", Guid.NewGuid(), null,
            "Assigned B", "B", DateTimeOffset.UtcNow);
        await using (var db = (NotificationDbContext)Contexts.Create("Notification", catalog.ConnectionString))
        { db.AddRange(noticeA, noticeB); await db.SaveChangesAsync(); }
        Assert.Equal(noticeA.Id, await NextHintAsync(socketA));
        Assert.Equal(noticeB.Id, await NextHintAsync(socketB));
        var connections = app.Services.GetRequiredService<NotificationConnections>();
        Assert.Equal(1, connections.DisconnectOlder(a, 2));
        using (var closedDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
        {
            try
            {
                // SignalR can send its own protocol Close (type 7) before closing the WebSocket.
                // A keepalive may already be buffered; neither is a new application notification.
                while (true)
                {
                    var buffer = new byte[8192];
                    var closed = await socketA.ReceiveAsync(buffer.AsMemory(), closedDeadline.Token);
                    if (closed.MessageType == WebSocketMessageType.Close) break;
                    foreach (var frame in Encoding.UTF8.GetString(buffer, 0, closed.Count)
                        .Split('\u001e', StringSplitOptions.RemoveEmptyEntries))
                    {
                        using var json = JsonDocument.Parse(frame);
                        Assert.Contains(json.RootElement.GetProperty("type").GetInt32(), new[] { 6, 7 });
                    }
                }
            }
            catch (WebSocketException) { }
        }
        var renewed = issuer.AdminToken("Technician", a, 2); directory.ActiveTokens.Add(renewed);
        using var renewedA = await ConnectAsync(address, renewed);
        Assert.Equal(0, connections.DisconnectOlder(a, 1));
        var otherSessionToken = issuer.AdminToken("Technician", a, 2); directory.ActiveTokens.Add(otherSessionToken);
        using var otherSession = await ConnectAsync(address, otherSessionToken);
        var session = Guid.Parse(new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(otherSessionToken)
            .Claims.Single(x => x.Type == "session_id").Value);
        Assert.Equal(1, connections.DisconnectSession(a, session));
        var nextNotice = NotificationRecord.UserEvent(a, Guid.NewGuid(), "WorkOrderAssigned", "WorkOrder", Guid.NewGuid(), null,
            "New session remains authorized", "A", DateTimeOffset.UtcNow);
        await using (var db = (NotificationDbContext)Contexts.Create("Notification", catalog.ConnectionString))
        { db.Add(nextNotice); await db.SaveChangesAsync(); }
        Assert.Equal(nextNotice.Id, await NextHintAsync(renewedA));
        await using (var db = (NotificationDbContext)Contexts.Create("Notification", catalog.ConnectionString))
        {
            var store = new PersonalNotificationStore(db);
            Assert.Equal(new[] { noticeA.Id, nextNotice.Id }.Order(),
                (await store.ListAsync(a, true, 1, default)).Items.Select(x => x.Id).Order());
            Assert.False(await store.MarkReadAsync(a, noticeB.Id, default));
            Assert.True(await store.MarkReadAsync(a, noticeA.Id, default));
        }
        socketA.Abort(); socketB.Abort(); await app.StopAsync();
    }

    [SqlServerFact]
    public async Task Channel_preference_snapshot_keeps_email_only_records_out_of_personal_feed_and_rowversion_rejects_stale_write()
    {
        await using var catalog = await TestDatabase.CreateAsync("Notification");
        await using var db = (NotificationDbContext)Contexts.Create("Notification", catalog.ConnectionString);
        await db.Database.MigrateAsync();
        var user = Guid.NewGuid();
        var preferences = new NotificationPreferences(db, TimeProvider.System);
        Assert.True(await preferences.SaveAsync(user, "WorkOrderAssigned", false, true, null, default));
        var version = (await preferences.ListAsync(user, default)).Single().RowVersion;
        var payload = new UserNotificationEvent("WorkOrderAssigned", "Recipient", user, "WorkOrder", Guid.NewGuid(), null, "Assigned", "Review");
        var message = payload.Envelope("Maintenance", 1, DateTimeOffset.UtcNow, Guid.NewGuid().ToString());
        await NotificationConsumer.StoreUserEventAsync(db, message, payload, [user], DateTimeOffset.UtcNow, default);
        await NotificationConsumer.StoreUserEventAsync(db, message, payload, [user], DateTimeOffset.UtcNow, default);
        db.ChangeTracker.Clear();
        var notification = await db.Notifications.AsNoTracking().SingleAsync();
        Assert.False(notification.IsInAppVisible); Assert.True(notification.IsEmailRequested);
        Assert.Empty((await new PersonalNotificationStore(db).ListAsync(user, false, 1, default)).Items);
        Assert.True(await preferences.SaveAsync(user, "WorkOrderAssigned", true, false, version, default));
        Assert.False(await preferences.SaveAsync(user, "WorkOrderAssigned", false, true, version, default));
        // Changes affect future events, not the saved channel decision for the old notification.
        Assert.False((await db.Notifications.AsNoTracking().SingleAsync()).IsInAppVisible);
    }

    private static async Task<ClientWebSocket> ConnectAsync(string address, string token)
    {
        var socket = new ClientWebSocket();
        await socket.ConnectAsync(new Uri(address.Replace("http://", "ws://", StringComparison.Ordinal) +
            "/hubs/notifications?access_token=" + Uri.EscapeDataString(token)), CancellationToken.None);
        await socket.SendAsync(Encoding.UTF8.GetBytes("{\"protocol\":\"json\",\"version\":1}\u001e").AsMemory(),
            WebSocketMessageType.Text, true, CancellationToken.None);
        var handshake = await FrameAsync(socket);
        Assert.StartsWith("{}", handshake);
        return socket;
    }
    private static async Task<string> FrameAsync(ClientWebSocket socket)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var buffer = new byte[8192];
        using var data = new MemoryStream();
        ValueWebSocketReceiveResult result;
        do { result = await socket.ReceiveAsync(buffer.AsMemory(), deadline.Token); data.Write(buffer, 0, result.Count); }
        while (!result.EndOfMessage);
        return Encoding.UTF8.GetString(data.ToArray());
    }
    private static async Task<Guid> NextHintAsync(ClientWebSocket socket)
    {
        for (var i = 0; i < 5; i++)
            foreach (var frame in (await FrameAsync(socket)).Split('\u001e', StringSplitOptions.RemoveEmptyEntries))
            {
                using var json = JsonDocument.Parse(frame);
                if (json.RootElement.TryGetProperty("target", out var target) && target.GetString() == "notificationsChanged")
                    return json.RootElement.GetProperty("arguments")[0].GetProperty("notificationId").GetGuid();
            }
        throw new InvalidOperationException("Realtime hint not received.");
    }
}
