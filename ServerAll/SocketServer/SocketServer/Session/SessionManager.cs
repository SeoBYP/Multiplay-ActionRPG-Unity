using System.Collections.Concurrent;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<SessionManager> _logger;
    private readonly ILogger<Session> _sessionLogger;

    public SessionManager(
        PacketDispatcher dispatcher,
        RoomManager roomManager,
        ILogger<SessionManager> logger,
        ILogger<Session> sessionLogger)
    {
        Instance = this;
        _dispatcher = dispatcher;
        _roomManager = roomManager;
        _logger = logger;
        _sessionLogger = sessionLogger;
    }

    public Session? CreateSession(Socket clientSocket, CancellationToken ct)
    {
        var id = Interlocked.Increment(ref _nextSessionId);
        var session = new Session(
            sessionId: id,
            socket: clientSocket,
            dispatcher: _dispatcher,
            roomManager: _roomManager,
            logger: _sessionLogger,
            onDisconnected: OnSessionDisconnected);

        if (!_sessions.TryAdd(id, session))
            return null;

        _logger.LogInformation("Session {SessionId} created", id);

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
        if (_sessions.TryRemove(sessionId, out _))
        {
            _roomManager.LeaveRoom(sessionId);
        }

        _logger.LogInformation("Session {SessionId} disconnected", sessionId);
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
        foreach (var (_, session) in _sessions)
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

    public int SessionCount => _sessions.Count;
}
