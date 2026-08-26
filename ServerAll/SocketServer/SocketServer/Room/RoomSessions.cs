using Microsoft.Extensions.Logging;
using Shared.Packet.Packets;

namespace Server.Room;

/// <summary>
/// 방 하나의 <b>연결 집합</b>. 지금 이 방에 붙어 있는 TCP 세션을 소유하고, 방 전체 브로드캐스트를 담당한다.
///
/// <para><b>참가자(RoomMember)와 다르다.</b> 세션은 연결이라 자주 죽고 되살아나지만,
/// 참가자와 그 액터는 재접속 유예 동안 그대로 살아 있다. 두 수명이 다르기 때문에 저장소도 다르다 —
/// 이 비대칭이 재접속을 가능하게 하는 구조 그 자체다.</para>
///
/// <para>정원(<see cref="MaxMembers"/>) 판정도 여기 있다 — "몇 명이 들어와 있나"는 연결의 질문이다.</para>
/// </summary>
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

    /// <summary>정원(게임 시작 시 확정된 참가자 수).</summary>
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

    /// <summary>
    /// 같은 UserId 의 <b>다른</b> 세션을 찾는다(재접속 인수용). 없으면 null.
    /// 끊김은 즉시 감지되지 않는다 — FIN 없이 사라지면 유휴 타임아웃까지 옛 세션이 방에 남는다(실측 63초).
    /// </summary>
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

    /// <summary>세션을 방에 등록한다. 정원 초과·중복이면 false.</summary>
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

    /// <summary>세션을 제거하고 그 UserId 를 돌려준다. 없던 세션이면 null.</summary>
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

    /// <summary>
    /// 방 전체에 보낸다. 전송은 fire-and-forget(세션의 송신 큐가 받는다) — 한 세션이 느려도 방이 멈추지 않는다.
    /// 예외를 삼키는 이유: 브로드캐스트 실패가 틱 루프나 핸들러를 죽이면 안 된다.
    /// </summary>
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
