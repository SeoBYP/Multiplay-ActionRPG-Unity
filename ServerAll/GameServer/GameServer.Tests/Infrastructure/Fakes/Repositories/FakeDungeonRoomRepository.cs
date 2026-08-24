using System.Collections.Concurrent;
using GameServer.Application.Domains.DungeonLobby;
using GameServer.Application.Domains.DungeonLobby.Interfaces;
using GameServer.Domain.Entities;

namespace GameServer.Tests.Infrastructure.Fakes.Repositories;

public class FakeDungeonRoomRepository : IDungeonRoomRepository
{
    private readonly ConcurrentDictionary<long, DungeonRoom> _rooms = new();
    private long _nextRoomId = 1;

    public Task<DungeonRoom?> CreateAsync(long hostId, string roomName, int maxPlayers = 4, string mapId = "", CancellationToken ct = default)
    {
        var room = DungeonRoom.Create(roomName, hostId, maxPlayers, mapId);
        var roomId = Interlocked.Increment(ref _nextRoomId);
        room.GetType().GetProperty("RoomId")?.SetValue(room, roomId);
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

    public Task<ActiveRoomsPage> GetActiveRoomsPageAsync(int offset, int limit, CancellationToken ct = default)
    {
        // 실제 저장소와 같은 계약: 최신순(RoomId 내림차순)으로 잘라 주고 총계는 페이지 크기와 무관.
        var active = _rooms.Values
            .Where(r => r.Status != RoomStatus.Closed)
            .OrderByDescending(r => r.RoomId)
            .ToList();

        var page = active.Skip(offset).Take(limit).ToList();
        return Task.FromResult(new ActiveRoomsPage(page, active.Count));
    }

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

    /// <summary>테스트에서 InvalidateCacheAsync 호출 횟수를 검증할 때 사용한다.</summary>
    public int InvalidateCacheCallCount { get; private set; }

    public Task InvalidateCacheAsync(long roomId, CancellationToken ct = default)
    {
        InvalidateCacheCallCount++;
        return Task.CompletedTask;
    }
}
