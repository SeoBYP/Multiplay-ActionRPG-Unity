using System.Collections.Concurrent;

namespace Server.Room;

public class RoomManager
{
    private long _nextRoomId = 1;
    private readonly ConcurrentDictionary<long, Room> _rooms = new();
    private readonly ConcurrentDictionary<ulong, long> _playerRooms = new();

    /// <summary>
    /// 방 생성
    /// </summary>
    public Room CreateRoom(long msgRoomId, List<long> msgPlayerIds)
    {
        var room = new Room(msgRoomId, msgPlayerIds); 
        
        if (!_rooms.TryAdd(msgRoomId, room))
        {
            Console.WriteLine($"[RoomManager] Failed to create room {msgRoomId}");
            return null;
        }
        Console.WriteLine($"[RoomManager] Room {msgRoomId} created (max: {msgPlayerIds.Count})");
        return room;
    }
    
    /// <summary>
    /// 플레이어 방 입장
    /// </summary>
    public bool JoinRoom(Session session, int? roomId = null)
    {
        // 기존 방 퇴장
        LeaveRoom(session.SessionId);
        
        // roomId가 없으면 새 방 생성
        Room? room;
        if (roomId.HasValue)
        {
            room = _rooms.GetValueOrDefault(roomId.Value);
            if (room == null)
            {
                Console.WriteLine($"[RoomManager] Room {roomId} not found");
                return false;
            }
        }
        else
        {
            // 자동 매칭: 빈 방 찾거나 새로 생성
            room = FindAvailableRoom();
        }
        
        if (room != null && room.Join(session))
        {
            _playerRooms[session.SessionId] = room.RoomId;
            return true;
        }

        return false;
    }


    /// <summary>
    /// 플레이어 방 퇴장
    /// </summary>
    public bool LeaveRoom(ulong sessionId)
    {
        if(!_playerRooms.TryRemove(sessionId, out long roomId))
            return false;

        var room = _rooms.GetValueOrDefault(roomId);
        if(room == null) 
            return false;
        
        room.Leave(sessionId);
        
        if (room.MemberCount == 0)
        {
            if (_rooms.TryRemove(roomId, out _))
            {
                Console.WriteLine($"[RoomManager] Room {roomId} removed (empty)");
            }
        }
        return true;
    }
    
    /// <summary>
    /// 플레이어가 현재 있는 방
    /// </summary>
    public Room? GetPlayerRoom(ulong sessionId)
    {
        if (_playerRooms.TryGetValue(sessionId, out long roomId))
        {
            return _rooms.GetValueOrDefault(roomId);
        }
        return null;
    }
    
    /// <summary>
    /// 입장 가능한 방 찾기 (자동 매칭용)
    /// </summary>
    public Room? FindAvailableRoom()
    {
        return _rooms.Values.FirstOrDefault(r => !r.IsFull);
    }


    /// <summary>
    /// 방 조회
    /// </summary>
    public Room? GetRoom(long roomId)
    {
        return _rooms.GetValueOrDefault(roomId);
    }

    /// <summary>
    /// 모든 방 목록
    /// </summary>
    public List<Room> GetAllRooms()
    {
        return _rooms.Values.ToList();
    }

    /// <summary>
    /// 방 개수
    /// </summary>
    public int RoomCount => _rooms.Count;
}