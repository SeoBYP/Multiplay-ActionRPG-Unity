using System.Collections.Concurrent;
using GameServer.Application.Domains.DungeonLobby;
using GameServer.Application.Domains.DungeonLobby.Interfaces;
using GameServer.Domain.Entities;

namespace GameServer.Tests.Fakes;

public class FakeDungeonRoomRepository : IDungeonRoomRepository
{
    private readonly ConcurrentDictionary<long, DungeonRoom> _rooms = new();
    private long _nextRoomId = 1;

    public Task<DungeonRoom?> CreateAsync(long hostId, string roomName, int maxPlayers = 4, CancellationToken ct = default)
    {
        var room = DungeonRoom.Create(roomName, hostId, maxPlayers);
        var roomId = Interlocked.Increment(ref _nextRoomId);
        room.SetRoomId(roomId);
        _rooms[roomId] = room;
        return Task.FromResult<DungeonRoom?>(room);
    }

    public Task<DungeonRoom?> GetByIdAsync(long roomId, CancellationToken ct = default)
    {
        _rooms.TryGetValue(roomId, out var room);
        return Task.FromResult(room);
    }

    public Task<DungeonRoom?> GetByUserIdAsync(long userId, CancellationToken ct = default)
        => Task.FromResult<DungeonRoom?>(null);

    public Task<IEnumerable<DungeonRoom>> GetAllActiveRoomsAsync(CancellationToken ct = default)
        => Task.FromResult<IEnumerable<DungeonRoom>>(_rooms.Values.Where(r => r.Status != RoomStatus.Closed).ToList());

    public Task<long> GetActiveRoomCountAsync(CancellationToken ct = default)
        => Task.FromResult((long)_rooms.Count(r => r.Value.Status != RoomStatus.Closed));

    public Task<bool> UpdateAsync(DungeonRoom room, CancellationToken ct = default)
    {
        if (!_rooms.ContainsKey(room.RoomId))
            return Task.FromResult(false);

        _rooms[room.RoomId] = room;
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(long roomId, CancellationToken ct = default)
        => Task.FromResult(_rooms.TryRemove(roomId, out _));

    public Task<JoinRoomAtomicResult> TryJoinRoomAsync(long userId, long roomId, CancellationToken ct = default)
    {
        if (!_rooms.TryGetValue(roomId, out var room))
            return Task.FromResult(JoinRoomAtomicResult.RoomNotFound);

        return Task.FromResult(room.Status == RoomStatus.Waiting
            ? JoinRoomAtomicResult.Success
            : JoinRoomAtomicResult.InvalidStatus);
    }
}
