using System.Collections.Concurrent;
using GameServer.Application.Domains.DungeonLobby.Interfaces;
using GameServer.Domain.Entities;

namespace GameServer.Tests.Infrastructure;

public class FakeDungeonRoomPlayerRepository : IDungeonRoomPlayerRepository
{
    private readonly ConcurrentDictionary<(long RoomId, long UserId), DungeonRoomPlayer> _players = new();
    private readonly ConcurrentDictionary<long, long> _userRoomMap = new();

    public Task<DungeonRoomPlayer> CreateAsync(long roomId, long userId, CancellationToken ct = default)
    {
        var player = DungeonRoomPlayer.Create(roomId, userId);
        _players[(roomId, userId)] = player;
        _userRoomMap[userId] = roomId;
        return Task.FromResult(player);
    }

    public Task<List<DungeonRoomPlayer>> GetPlayersByRoomIdAsync(long roomId, CancellationToken ct = default)
    {
        var players = _players.Values
            .Where(player => player.RoomId == roomId)
            .OrderBy(player => player.JoinedAt)
            .ToList();
        return Task.FromResult(players);
    }

    public Task<DungeonRoomPlayer?> GetByUserIdAsync(long userId, CancellationToken ct = default)
    {
        if (!_userRoomMap.TryGetValue(userId, out var roomId))
            return Task.FromResult<DungeonRoomPlayer?>(null);

        _players.TryGetValue((roomId, userId), out var player);
        return Task.FromResult(player);
    }

    public Task<bool> RemoveAsync(long roomId, long userId, CancellationToken ct = default)
    {
        var removed = _players.TryRemove((roomId, userId), out _);
        if (removed)
            _userRoomMap.TryRemove(userId, out _);
        return Task.FromResult(removed);
    }

    public Task<bool> RemoveByRoomIdAsync(long roomId, CancellationToken ct = default)
    {
        var removed = false;
        var players = _players.Values.Where(player => player.RoomId == roomId).ToList();
        foreach (var player in players)
        {
            removed |= _players.TryRemove((player.RoomId, player.UserId), out _);
            _userRoomMap.TryRemove(player.UserId, out _);
        }

        return Task.FromResult(removed);
    }
}
