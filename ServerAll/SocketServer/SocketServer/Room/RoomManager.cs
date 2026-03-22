using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Server.Room;

public class RoomManager
{
    private readonly ConcurrentDictionary<long, Room> _rooms = new();
    private readonly ConcurrentDictionary<ulong, long> _playerRooms = new();
    private readonly ILogger<RoomManager> _logger;
    private readonly ILogger<Room> _roomLogger;

    public RoomManager(ILogger<RoomManager> logger, ILogger<Room> roomLogger)
    {
        _logger = logger;
        _roomLogger = roomLogger;
    }

    public Room? CreateRoom(long msgRoomId, List<long> msgPlayerIds)
    {
        var room = new Room(msgRoomId, msgPlayerIds, _roomLogger);

        if (!_rooms.TryAdd(msgRoomId, room))
        {
            _logger.LogWarning("Failed to create room {RoomId}", msgRoomId);
            return null;
        }

        _logger.LogInformation("Room {RoomId} created with {MaxPlayers} players", msgRoomId, msgPlayerIds.Count);
        return room;
    }

    public bool JoinRoom(Session session, int? roomId = null)
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
            return true;
        }

        return false;
    }

    public bool LeaveRoom(ulong sessionId)
    {
        if (!_playerRooms.TryRemove(sessionId, out long roomId))
            return false;

        var room = _rooms.GetValueOrDefault(roomId);
        if (room == null)
            return false;

        room.Leave(sessionId);

        if (room.MemberCount == 0 && _rooms.TryRemove(roomId, out _))
        {
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

    public List<Room> GetAllRooms()
    {
        return _rooms.Values.ToList();
    }

    public int RoomCount => _rooms.Count;
}
