using System.Collections.Concurrent;
using GameServer.Application.Domains.DungeonLobby.Interfaces;

namespace GameServer.Tests.Infrastructure.Fakes.Repositories;

public class FakeRoomReadyStore : IRoomReadyStore
{
    private readonly ConcurrentDictionary<long, HashSet<long>> _ready = new();

    public Task SetReadyAsync(long roomId, long userId, bool isReady, CancellationToken ct = default)
    {
        var set = _ready.GetOrAdd(roomId, _ => new HashSet<long>());
        lock (set)
        {
            if (isReady) set.Add(userId);
            else set.Remove(userId);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlySet<long>> GetReadyUserIdsAsync(long roomId, CancellationToken ct = default)
    {
        if (!_ready.TryGetValue(roomId, out var set))
            return Task.FromResult<IReadOnlySet<long>>(new HashSet<long>());

        lock (set)
        {
            return Task.FromResult<IReadOnlySet<long>>(new HashSet<long>(set));
        }
    }

    public async Task<IReadOnlyDictionary<long, IReadOnlySet<long>>> GetReadyUserIdsAsync(
        IReadOnlyCollection<long> roomIds, CancellationToken ct = default)
    {
        var result = new Dictionary<long, IReadOnlySet<long>>(roomIds.Count);
        foreach (var roomId in roomIds.Distinct())
            result[roomId] = await GetReadyUserIdsAsync(roomId, ct);
        return result;
    }

    public Task ClearAsync(long roomId, CancellationToken ct = default)
    {
        _ready.TryRemove(roomId, out _);
        return Task.CompletedTask;
    }
}
