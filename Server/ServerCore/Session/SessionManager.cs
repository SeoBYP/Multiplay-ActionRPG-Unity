using System.Collections.Concurrent;

namespace ServerCore;

public sealed class SessionManager
{
    public readonly ConcurrentDictionary<long, Session> _sessions = new();

    public bool Add(Session session)
    {
        return _sessions.TryAdd(session.SessionId, session);
    }

    public bool Remove(long sessionId)
    {
        if (_sessions.TryRemove(sessionId, out var session))
        {
            session.Disconnect();
            return true;
        }
        return false;
    }

    public Session? Get(long sessionId)
    {
        return _sessions.GetValueOrDefault(sessionId);
    }

    public void Start(Session session, CancellationToken serverCt)
    {
        _ = session.RunAsync(serverCt);
    }
    
    public void StopAll()
    {
        foreach (var s in _sessions.Values)
            s.Disconnect();
        _sessions.Clear();
    }
}