using System.Collections.Concurrent;
using System.Net.Sockets;
using Server.PacketHandler;
using Server.Room;
using Shared.Packet.Packets;


public sealed class SessionManager
{
    public static SessionManager? Instance { get; private set; }

    private ulong _nextSessionId = 0;
    
    private readonly ConcurrentDictionary<ulong, Session> _sessions = new();

    private readonly PacketDispatcher _dispatcher; 
    private readonly RoomManager _roomManager;
    public SessionManager(PacketDispatcher dispatcher, RoomManager roomManager)
    {
        Instance = this;
        _dispatcher = dispatcher;
        _roomManager = roomManager;
    }
    
    public Session? CreateSession(Socket clientSocket, CancellationToken ct)
    {
        var id = Interlocked.Increment(ref _nextSessionId);
        var session = new Session(
            sessionId: id,
            socket: clientSocket,
            dispatcher: _dispatcher,  // ✅ Dispatcher 주입
            roomManager: _roomManager,
            onDisconnected: OnSessionDisconnected);

        if (!_sessions.TryAdd(id, session))
            return null;

        Console.WriteLine($"Session {id} created.");
        
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
        if (_sessions.TryRemove(sessionId, out var session))
        {
            _roomManager.LeaveRoom(sessionId);  // Room에서도 퇴장
        }
        Console.WriteLine($"[SessionManager] Session {sessionId} disconnected");
    }

    public Session? Get(ulong sessionId)
    {
        return _sessions.GetValueOrDefault(sessionId);
    }

    public Session? GetWithNickname(string nickname)
    {
        return _sessions.Values.FirstOrDefault(s => s.Nickname == nickname);
    }
    
    public void BroadcastAll(Packet packet, CancellationToken ct)
    {
        foreach (var (key, session) in _sessions)
        {
            _ = session.SendPacketAsync(packet, ct);
        }
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
