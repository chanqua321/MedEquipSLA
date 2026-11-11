using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace MedEquipSLA.Notification.Infrastructure.Realtime;

public sealed class NotificationHub(NotificationConnections connections) : Hub
{
    public override Task OnConnectedAsync()
    {
        if (!Guid.TryParse(Context.UserIdentifier, out var user) ||
            !Guid.TryParse(Context.User?.FindFirstValue("session_id"), out var session) || session == Guid.Empty ||
            !long.TryParse(Context.User?.FindFirstValue("authorization_version"), out var version) || version < 1 ||
            !connections.Register(Context.ConnectionId, user, session, version, Context.Abort))
        { Context.Abort(); return Task.CompletedTask; }
        return base.OnConnectedAsync();
    }
    public override Task OnDisconnectedAsync(Exception? exception)
    { connections.Remove(Context.ConnectionId); return base.OnDisconnectedAsync(exception); }
}
public sealed class NotificationUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) =>
        Guid.TryParse(connection.User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) && id != Guid.Empty ? id.ToString() : null;
}
