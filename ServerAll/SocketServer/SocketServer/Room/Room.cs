using Server.Player;
using Shared.Infrastructure.Messages;

namespace Server.Room;

using Microsoft.Extensions.Logging;
using Shared.Packet.Packets;

public class Room
{
    public long RoomId { get; private set; }
    public int MaxMembers { get; private set; }

    /// <summary>플레이 중인 맵 식별자. 스폰 레이아웃 선택에 사용. CreateRoom 에서 설정.</summary>
    public string MapId { get; set; } = Shared.Infrastructure.Spawn.MapIds.Default;

    private readonly Dictionary<ulong, Session> _playerSessions = new();
    private readonly Dictionary<long, PlayerState> _playerStates = new();
    private readonly HashSet<long> _expectedUserIds;
    private readonly ILogger<Room> _logger;

    // 서버 권위 GameplayEffect InstanceId 발급기 (방 단위, 스레드 안전).
    private int _nextEffectInstanceId;

    /// <summary>활성 Effect 인스턴스에 부여할 서버 권위 InstanceId를 1씩 증가시켜 반환한다.</summary>
    public int NextEffectInstanceId() => System.Threading.Interlocked.Increment(ref _nextEffectInstanceId);
    

    public int MemberCount
    {
        get
        {
            lock (_playerSessions)
            {
                return _playerSessions.Count;
            }
        }
    }

    public bool IsFull => MemberCount >= MaxMembers;

    public Room(long roomId, IReadOnlyList<PlayerInfo> expectedUserIds, ILogger<Room> logger)
    {
        RoomId = roomId;
        MaxMembers = expectedUserIds.Count;
        _expectedUserIds = new HashSet<long>();
        foreach (var playerInfo in expectedUserIds)
        {
            _expectedUserIds.Add(playerInfo.UserId);
        }
        _logger = logger;
    }

    public bool IsExpectedPlayer(long userId) => _expectedUserIds.Contains(userId);

    public Session? GetSession(ulong sessionId)
    {
        lock (_playerSessions)
        {
            return _playerSessions.GetValueOrDefault(sessionId);
        }
    }

    public PlayerState? GetPlayerState(long userId)
    {
        lock (_playerStates)
        {
            return _playerStates.GetValueOrDefault(userId);
        }
    }

    public bool Join(Session session)
    {
        try
        {
            lock (_playerSessions)
            {
                if (IsFull)
                {
                    _logger.LogWarning("Room {RoomId} is full. Session {SessionId} cannot join", RoomId, session.SessionId);
                    return false;
                }

                if (_playerSessions.ContainsKey(session.SessionId))
                {
                    _logger.LogWarning("Session {SessionId} is already in room {RoomId}", session.SessionId, RoomId);
                    return false;
                }

                _playerSessions.Add(session.SessionId, session);
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
            lock (_playerSessions)
            {
                if (!_playerSessions.Remove(sessionId))
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

    public void InitPlayerState(long userId, string nickname, int spawnIndex, float spawnX, float spawnY, float spawnZ, float rotY)
    {
        lock (_playerStates)
        {
            var playerState = new PlayerState
            {
                UserId = userId,
                Nickname = nickname,
                SpawnIndex = spawnIndex,
                PosX = spawnX,
                PosY = spawnY,
                PosZ = spawnZ,
                RotY = rotY,
                LastMovedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            _playerStates[userId] = playerState;

            _logger.LogInformation(
                "Initialized player state for User {UserId} ({Nickname}) slot {SpawnIndex} at ({SpawnX}, {SpawnY}, {SpawnZ}) in Room {RoomId}",
                userId,
                nickname,
                spawnIndex,
                spawnX,
                spawnY,
                spawnZ,
                RoomId);
        }
    }

    public void UpdatePlayerState(long userId, float x, float y, float z, float rotY, long timestamp)
    {
        lock (_playerStates)
        {
            if (_playerStates.TryGetValue(userId, out var playerState))
            {
                playerState.PosX = x;
                playerState.PosY = y;
                playerState.PosZ = z;
                playerState.RotY = rotY;
                playerState.LastMovedAt = timestamp;
            }
            else
            {
                _logger.LogWarning("Player state not found for User {UserId} in Room {RoomId}", userId, RoomId);
            }
        }
    }

    public IReadOnlyList<PlayerState> GetAllPlayerStates()
    {
        lock (_playerStates)
        {
            return _playerStates.Values.ToList();
        }
    }

    public void Broadcast(Packet packet, ulong? excludeSessionId = null)
    {
        try
        {
            lock (_playerSessions)
            {
                foreach (var (sessionId, session) in _playerSessions)
                {
                    if (excludeSessionId.HasValue && sessionId == excludeSessionId.Value)
                        continue;

                    _ = session.SendPacketAsync(packet);
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to broadcast in room {RoomId}", RoomId);
        }
    }
}
