
namespace Server.Room;

using Microsoft.Extensions.Logging;
using Shared.Packet.Packets;

public class Room
{
    public long RoomId { get; private set; }
    public int MaxMembers { get; private set; }

    private readonly Dictionary<ulong, Session> _players = new();
    private readonly HashSet<long> _expectedUserIds;
    private readonly ILogger<Room> _logger;

    public int MemberCount
    {
        get
        {
            lock (_players)
            {
                return _players.Count;
            }
        }
    }

    public bool IsFull => MemberCount >= MaxMembers;

    public Room(long roomId, List<long> expectedUserIds, ILogger<Room> logger)
    {
        RoomId = roomId;
        MaxMembers = expectedUserIds.Count;
        _expectedUserIds = new HashSet<long>(expectedUserIds);
        _logger = logger;
    }

    public bool IsExpectedPlayer(long userId) => _expectedUserIds.Contains(userId);

    public bool Join(Session session)
    {
        try
        {
            lock (_players)
            {
                if (IsFull)
                {
                    _logger.LogWarning("Room {RoomId} is full. Session {SessionId} cannot join", RoomId, session.SessionId);
                    return false;
                }

                if (_players.ContainsKey(session.SessionId))
                {
                    _logger.LogWarning("Session {SessionId} is already in room {RoomId}", session.SessionId, RoomId);
                    return false;
                }

                _players.Add(session.SessionId, session);
                _logger.LogInformation(
                    "Session {SessionId} joined room {RoomId}. Members: {MemberCount}/{MaxMembers}",
                    session.SessionId,
                    RoomId,
                    MemberCount,
                    MaxMembers);

                return true;
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to join room {RoomId}", RoomId);
            throw;
        }
    }

    public bool Leave(ulong sessionId)
    {
        try
        {
            lock (_players)
            {
                if (!_players.Remove(sessionId))
                {
                    _logger.LogWarning("Session {SessionId} is not in room {RoomId}", sessionId, RoomId);
                    return false;
                }

                _logger.LogInformation(
                    "Session {SessionId} left room {RoomId}. Members: {MemberCount}/{MaxMembers}",
                    sessionId,
                    RoomId,
                    MemberCount,
                    MaxMembers);

                return true;
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to leave room {RoomId}", RoomId);
            throw;
        }
    }

    public void Broadcast(Packet packet, ulong? excludeSessionId = null)
    {
        try
        {
            lock (_players)
            {
                int sentCount = 0;

                foreach (var (sessionId, session) in _players)
                {
                    if (excludeSessionId.HasValue && sessionId == excludeSessionId.Value)
                        continue;

                    _ = session.SendPacketAsync(packet);
                    sentCount++;
                }

                _logger.LogInformation("Broadcasted packet to {SentCount} members in room {RoomId}", sentCount, RoomId);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to broadcast in room {RoomId}", RoomId);
            throw;
        }
    }
}
