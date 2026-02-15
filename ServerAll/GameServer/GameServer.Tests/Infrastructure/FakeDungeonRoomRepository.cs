// GameServer.Tests/Fakes/FakeDungeonRoomRepository.cs
using System.Collections.Concurrent;
using GameServer.Domain.Entities;
using GameServer.Infrastructure.Interfaces.DungeonRoom;

namespace GameServer.Tests.Fakes;

public class FakeDungeonRoomRepository : IDungeonRoomRepository
{
    private readonly ConcurrentDictionary<long, DungeonRoom> _rooms = new();
    private readonly ConcurrentDictionary<long, long> _userRoomMapping = new();  // UserId → RoomId
    private long _nextRoomId = 1;

    public Task<DungeonRoom?> CreateAsync(long hostId, string roomName, int maxPlayers = 4)
    {
        var room = DungeonRoom.Create(roomName, hostId, maxPlayers);
        var roomId = Interlocked.Increment(ref _nextRoomId);
        room.SetRoomId(roomId);
        
        _rooms[roomId] = room;
        _userRoomMapping[hostId] = roomId;
        
        return Task.FromResult<DungeonRoom?>(room);
    }

    public Task<DungeonRoom?> GetByIdAsync(long roomId)
    {
        _rooms.TryGetValue(roomId, out var room);
        return Task.FromResult(room);
    }

    public Task<DungeonRoom?> GetByUserIdAsync(long userId)
    {
        if (!_userRoomMapping.TryGetValue(userId, out var roomId))
            return Task.FromResult<DungeonRoom?>(null);
        
        _rooms.TryGetValue(roomId, out var room);
        return Task.FromResult(room);
    }

    public Task<IEnumerable<DungeonRoom>> GetAllActiveRoomsAsync()
    {
        var activeRooms = _rooms.Values
            .Where(r => r.Status != RoomStatus.Closed)
            .ToList();
        
        return Task.FromResult<IEnumerable<DungeonRoom>>(activeRooms);
    }

    public Task<long> GetActiveRoomCountAsync()
    {
        return Task.FromResult((long)_rooms.Count(r => r.Value.Status != RoomStatus.Closed));
    }

    public Task<bool> UpdateAsync(DungeonRoom room)
    {
        if (!_rooms.ContainsKey(room.RoomId))
            return Task.FromResult(false);
        
        // 기존 매핑 삭제
        var oldMappings = _userRoomMapping
            .Where(kvp => kvp.Value == room.RoomId)
            .Select(kvp => kvp.Key)
            .ToList();
        
        foreach (var userId in oldMappings)
        {
            _userRoomMapping.TryRemove(userId, out _);
        }
        
        // 새 매핑 추가
        foreach (var userId in room.CurrentPlayers)
        {
            _userRoomMapping[userId] = room.RoomId;
        }
        
        _rooms[room.RoomId] = room;
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(long roomId)
    {
        if (!_rooms.TryRemove(roomId, out var room))
            return Task.FromResult(false);
        
        // 매핑 삭제
        foreach (var userId in room.CurrentPlayers)
        {
            _userRoomMapping.TryRemove(userId, out _);
        }
        
        return Task.FromResult(true);
    }
}