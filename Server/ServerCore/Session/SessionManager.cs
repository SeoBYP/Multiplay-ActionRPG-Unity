using System.Collections.Concurrent;
using System.Net.Sockets;

namespace ServerCore;

public sealed class SessionManager
{
    private ulong _nextSessionId = 0;
    public readonly ConcurrentDictionary<ulong, Session> _sessions = new();


    public Session? CreateSession(Socket clientSocket, CancellationToken ct)
    {
        var id = Interlocked.Increment(ref _nextSessionId);
        var session = new Session(sessionId: id, socket: clientSocket, onDisconnected: OnSessionDisconnected);

        if (!_sessions.TryAdd(id, session))
            return null;

        Console.WriteLine($"Session {id} created.");

        // Start() 절대 호출하지 말 것
        _ = session.RunAsync(ct);

        return session;
    }
    
    public bool Remove(ulong sessionId)
    {
        if (_sessions.TryRemove(sessionId, out var session))
        {
            session.Disconnect();
            return true;
        }
        return false;
    }
    

    private void OnSessionDisconnected(ulong sessionId)
    {
        _sessions.TryRemove(sessionId, out _);
    }
    
    public Session? Get(ulong sessionId)
    {
        return _sessions.GetValueOrDefault(sessionId);
    }

    public void Clear()
    {
        foreach (var s in _sessions.Values)
            s.Disconnect();
        _sessions.Clear();
    }
}