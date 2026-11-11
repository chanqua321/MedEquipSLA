using System.Collections.Concurrent;

namespace MedEquipSLA.Notification.Infrastructure.Realtime;

public sealed class NotificationConnections
{
    private sealed record Connection(Guid User, Guid Session, long Version, Action Abort);
    private readonly ConcurrentDictionary<string, Connection> connections = new();
    private readonly Dictionary<Guid, long> minimumVersions = [];
    private readonly HashSet<(Guid User, Guid Session)> revokedSessions = [];
    private readonly object gate = new();
    public bool Register(string connection, Guid user, Guid session, long version, Action abort)
    {
        lock (gate)
        {
            if (revokedSessions.Contains((user, session)) || minimumVersions.TryGetValue(user, out var minimum) && version < minimum) return false;
            connections[connection] = new(user, session, version, abort); return true;
        }
    }
    public void Remove(string connection) => connections.TryRemove(connection, out _);
    public int DisconnectSession(Guid user, Guid session)
    {
        if (user == Guid.Empty || session == Guid.Empty) throw new ArgumentException("User and session required.");
        Connection[] old;
        lock (gate)
        {
            revokedSessions.Add((user, session));
            old = connections.Values.Where(x => x.User == user && x.Session == session).ToArray();
        }
        foreach (var connection in old) connection.Abort();
        return old.Length;
    }
    public int DisconnectOlder(Guid user, long version)
    {
        if (user == Guid.Empty || version < 1) throw new ArgumentException("User and authorization version required.");
        Connection[] old;
        lock (gate)
        {
            minimumVersions[user] = Math.Max(minimumVersions.GetValueOrDefault(user), version);
            old = connections.Values.Where(x => x.User == user && x.Version < minimumVersions[user]).ToArray();
        }
        foreach (var connection in old) connection.Abort();
        return old.Length;
    }
}
