using System.Collections.Concurrent;
using System.Net.Sockets;
using Server.Room;
using ServerCore.Protocol;


public sealed class SessionManager
{
    private ulong _nextSessionId = 0;
    
    private readonly ConcurrentDictionary<ulong, Session> _sessions = new();
    private readonly RoomManager _roomManager;

    public SessionManager()
    {
        _roomManager = new RoomManager();
    }
    
    public Session? CreateSession(Socket clientSocket, CancellationToken ct)
    {
        var id = Interlocked.Increment(ref _nextSessionId);
        var session = new Session(sessionId: id, socket: clientSocket, roomManager: _roomManager,
            onDisconnected: OnSessionDisconnected);

        if (!_sessions.TryAdd(id, session))
            return null;

        Console.WriteLine($"Session {id} created.");

        _roomManager.JoinRoom(session);
        
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
        // ✅ Room에서 자동 퇴장
        _roomManager.LeaveRoom(sessionId);

        _sessions.TryRemove(sessionId, out _);
        
        Console.WriteLine($"[SessionManager] Session {sessionId} disconnected");
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
    
    /// <summary>
    /// 현재 접속자 수
    /// </summary>
    public int SessionCount => _sessions.Count;
}