using Microsoft.Extensions.Logging;
using Shared.Packet.Packets;

namespace Server.Room;

// 연결 집합. 참가자(RoomMember)와 수명이 다르다 — 세션은 자주 죽고 되살아나지만 액터는 재접속 유예 동안 유지된다.
public sealed class RoomSessions
{
    private readonly Dictionary<ulong, Session> _sessions = new();
    private readonly long _roomId;
    private readonly ILogger _logger;

    public RoomSessions(long roomId, int maxMembers, ILogger logger)
    {
        _roomId = roomId;
        MaxMembers = maxMembers;
        _logger = logger;
    }

    public int MaxMembers { get; }

    public int Count
    {
        get { lock (_sessions) return _sessions.Count; }
    }

    public bool IsFull => Count >= MaxMembers;

    public Session? Get(ulong sessionId)
    {
        lock (_sessions) return _sessions.GetValueOrDefault(sessionId);
    }

    // 끊김은 즉시 감지되지 않는다 — FIN 없이 사라지면 유휴 타임아웃까지 옛 세션이 방에 남는다.
    public Session? FindByUserId(long userId, ulong exceptSessionId)
    {
        if (userId <= 0) return null;
        lock (_sessions)
        {
            foreach (var kv in _sessions)
                if (kv.Key != exceptSessionId && kv.Value.UserId == userId)
                    return kv.Value;
        }
        return null;
    }

    public bool Add(Session session)
    {
        lock (_sessions)
        {
            if (_sessions.Count >= MaxMembers)
            {
                _logger.LogWarning("Room {RoomId} is full. Session {SessionId} cannot join", _roomId, session.SessionId);
                return false;
            }

            if (!_sessions.TryAdd(session.SessionId, session))
            {
                _logger.LogWarning("Session {SessionId} is already in room {RoomId}", session.SessionId, _roomId);
                return false;
            }

            _logger.LogInformation(
                "Session {SessionId} joined room {RoomId}. Members: {MemberCount}/{MaxMembers}",
                session.SessionId, _roomId, _sessions.Count, MaxMembers);
            return true;
        }
    }

    public long? Remove(ulong sessionId)
    {
        lock (_sessions)
        {
            if (!_sessions.Remove(sessionId, out var session))
            {
                _logger.LogWarning("Session {SessionId} is not in room {RoomId}", sessionId, _roomId);
                return null;
            }

            _logger.LogInformation(
                "Session {SessionId} left room {RoomId}. Members: {MemberCount}/{MaxMembers}",
                sessionId, _roomId, _sessions.Count, MaxMembers);
            return session.UserId;
        }
    }

    // fire-and-forget(세션 송신 큐가 받는다). 브로드캐스트 실패가 틱 루프·핸들러를 죽이면 안 되어 예외를 삼킨다.
    public void Broadcast(Packet packet, ulong? excludeSessionId = null)
    {
        try
        {
            lock (_sessions)
            {
                foreach (var (sessionId, session) in _sessions)
                {
                    if (excludeSessionId.HasValue && sessionId == excludeSessionId.Value)
                        continue;

                    _ = session.SendPacketAsync(packet);
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to broadcast in room {RoomId}", _roomId);
        }
    }
}
