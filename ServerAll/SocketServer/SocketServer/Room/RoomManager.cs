using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Shared.Infrastructure.Messages;
using Shared.Infrastructure.Spawn;
using Shared.Packet.Packets;

namespace Server.Room;

public class RoomManager
{
    private readonly ConcurrentDictionary<long, Room> _rooms = new();
    private readonly ConcurrentDictionary<ulong, long> _playerRooms = new();
    private readonly ConcurrentDictionary<long, long> _userRoomIndex = new();
    private readonly ILogger<RoomManager> _logger;
    private readonly ILogger<Room> _roomLogger;
    private readonly IRoomLifecyclePublisher _lifecycleQueue;

    public RoomManager(
        ILogger<RoomManager> logger,
        ILogger<Room> roomLogger,
        IRoomLifecyclePublisher lifecycleQueue)
    {
        _logger = logger;
        _roomLogger = roomLogger;
        _lifecycleQueue = lifecycleQueue;
    }

    private readonly ConcurrentDictionary<long, GameStartRequestedMessage> _roomMessages = new();

    public Room? CreateRoom(long msgRoomId, IReadOnlyList<PlayerInfo> msgPlayerIds, GameStartRequestedMessage message)
    {
        var room = new Room(msgRoomId, msgPlayerIds, _roomLogger);

        if (!_rooms.TryAdd(msgRoomId, room))
        {
            _logger.LogWarning("Failed to create room {RoomId}", msgRoomId);
            return null;
        }

        _roomMessages[msgRoomId] = message;
        room.MapId = message.MapId;
        var layout = SpawnLayoutTable.Get(message.MapId);
        foreach (var playerInfo in message.PlayerInfos)
        {
            _userRoomIndex[playerInfo.UserId] = msgRoomId;
            var spawn = SpawnResolver.Resolve(layout, playerInfo.SpawnIndex);
            room.InitPlayerState(
                playerInfo.UserId, playerInfo.Nickname, playerInfo.SpawnIndex,
                spawn.X, spawn.Y, spawn.Z, spawn.RotY);
        }

        // 몬스터 초기 스폰(서버 권위) — 맵 경계도 함께 보관(이동 clamp 기준).
        room.SpawnMonsters(layout.Monsters, layout.Bounds);

        _logger.LogInformation("Room {RoomId} created with {MaxPlayers} players", msgRoomId, msgPlayerIds.Count);
        return room;
    }

    public GameStartRequestedMessage? GetRoomMessage(long roomId)
    {
        return _roomMessages.GetValueOrDefault(roomId);
    }

    public bool JoinRoom(Session session, long? roomId = null)
    {
        LeaveRoom(session.SessionId);

        Room? room;
        if (roomId.HasValue)
        {
            room = _rooms.GetValueOrDefault(roomId.Value);
            if (room == null)
            {
                _logger.LogWarning("Room {RoomId} not found", roomId.Value);
                return false;
            }
        }
        else
        {
            room = FindAvailableRoom();
        }

        if (room != null && room.Join(session))
        {
            _playerRooms[session.SessionId] = room.RoomId;
            session.Room = room;
            return true;
        }

        return false;
    }

    public bool LeaveRoom(Session session)
    {
        if (!_playerRooms.TryRemove(session.SessionId, out long roomId))
            return false;

        var room = _rooms.GetValueOrDefault(roomId);
        if (room == null)
            return false;

        var userId = session.UserId;
        room.Leave(session.SessionId);
        session.Room = null;
        if (userId > 0)
        {
            room.Broadcast(new S_PlayerLeft
            {
                UserId = userId
            });
        }

        PublishPlayerLeft(room, roomId, userId);
        return true;
    }

    public bool LeaveRoom(ulong sessionId)
    {
        if (!_playerRooms.TryRemove(sessionId, out long roomId))
            return false;

        var room = _rooms.GetValueOrDefault(roomId);
        if (room == null)
            return false;

        var session = room.GetSession(sessionId);
        var userId = session?.UserId ?? 0;
        room.Leave(sessionId);
        if (userId > 0)
        {
            room.Broadcast(new S_PlayerLeft
            {
                UserId = userId
            });
        }

        PublishPlayerLeft(room, roomId, userId);
        return true;
    }

    /// <summary>
    /// 퇴장 후처리: 빈 방이면 메모리에서 제거하고, 인증된 유저(UserId>0)면
    /// GameServer에 PlayerLeftRoomMessage를 발행한다(RoomEmptied 포함).
    /// 빈 방 여부와 무관하게 항상 발행해야 GameServer가 해당 유저 association을 정리한다.
    /// </summary>
    private void PublishPlayerLeft(Room room, long roomId, long userId)
    {
        var emptied = room.MemberCount == 0;
        if (emptied && _rooms.TryRemove(roomId, out _))
        {
            RemoveUserRoomIndexes(roomId);
            _roomMessages.TryRemove(roomId, out _);
            _logger.LogInformation("Room {RoomId} removed because it is empty", roomId);
        }

        if (userId > 0)
        {
            _ = _lifecycleQueue.EnqueueAsync(new PlayerLeftRoomMessage
            {
                RoomId = roomId,
                UserId = userId,
                RoomEmptied = emptied
            });
        }
    }

    public Room? GetPlayerRoom(ulong sessionId)
    {
        if (_playerRooms.TryGetValue(sessionId, out long roomId))
        {
            return _rooms.GetValueOrDefault(roomId);
        }

        return null;
    }

    public Room? FindAvailableRoom()
    {
        return _rooms.Values.FirstOrDefault(r => !r.IsFull);
    }

    public Room? GetRoom(long roomId)
    {
        return _rooms.GetValueOrDefault(roomId);
    }

    public Room? GetAssignedRoom(long userId)
    {
        if (!_userRoomIndex.TryGetValue(userId, out var roomId))
        {
            return null;
        }

        return _rooms.GetValueOrDefault(roomId);
    }

    public List<Room> GetAllRooms()
    {
        return _rooms.Values.ToList();
    }

    private void RemoveUserRoomIndexes(long roomId)
    {
        if (!_roomMessages.TryGetValue(roomId, out var message))
        {
            return;
        }

        foreach (var playerInfo in message.PlayerInfos)
        {
            _userRoomIndex.TryRemove(playerInfo.UserId, out _);
        }
    }

    public int RoomCount => _rooms.Count;
}
