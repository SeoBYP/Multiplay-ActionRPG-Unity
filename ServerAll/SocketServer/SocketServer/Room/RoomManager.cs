using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Shared.Infrastructure.Messages;
using Shared.Packet.Packets;

namespace Server.Room;

public class RoomManager
{
    private readonly ConcurrentDictionary<long, Room> _rooms = new();
    private readonly ConcurrentDictionary<ulong, long> _playerRooms = new();
    private readonly ConcurrentDictionary<long, long> _userRoomIndex = new();
    private readonly ILogger<RoomManager> _logger;
    private readonly ILogger<Room> _roomLogger;

    public RoomManager(ILogger<RoomManager> logger, ILogger<Room> roomLogger)
    {
        _logger = logger;
        _roomLogger = roomLogger;
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
        foreach (var playerInfo in message.PlayerInfos)
        {
            _userRoomIndex[playerInfo.UserId] = msgRoomId;
            var spawn = ResolveSpawn(playerInfo.SpawnIndex);
            room.InitPlayerState(playerInfo.UserId, playerInfo.Nickname, spawn.X, spawn.Y, spawn.Z);
        }

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

        room.Leave(session.SessionId);
        session.Room = null;
        if (session.UserId > 0)
        {
            room.Broadcast(new S_PlayerLeft
            {
                UserId = session.UserId
            });
        }

        if (room.MemberCount == 0 && _rooms.TryRemove(roomId, out _))
        {
            RemoveUserRoomIndexes(roomId);
            _roomMessages.TryRemove(roomId, out _);
            _logger.LogInformation("Room {RoomId} removed because it is empty", roomId);
        }

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
        room.Leave(sessionId);
        if (session is { UserId: > 0 })
        {
            room.Broadcast(new S_PlayerLeft
            {
                UserId = session.UserId
            });
        }

        if (room.MemberCount == 0 && _rooms.TryRemove(roomId, out _))
        {
            RemoveUserRoomIndexes(roomId);
            _roomMessages.TryRemove(roomId, out _);
            _logger.LogInformation("Room {RoomId} removed because it is empty", roomId);
        }

        return true;
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

    private static (float X, float Y, float Z) ResolveSpawn(int spawnIndex)
    {
        return spawnIndex switch
        {
            0 => (0f, 0f, 0f),
            1 => (2f, 0f, 0f),
            2 => (-2f, 0f, 0f),
            3 => (0f, 0f, 2f),
            4 => (0f, 0f, -2f),
            _ => (spawnIndex * 1.5f, 0f, 0f)
        };
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
