using GameServer.Application.Domains.DungeonLobby.Interfaces;
using StackExchange.Redis;

namespace GameServer.Infrastructure.Domains.DungeonRoom;

/// <summary>
/// <see cref="IRoomReadyStore"/> 의 Redis Set 구현.
/// 키 = <c>RedisKeys.DungeonRoomReady(roomId)</c>, 원소 = userId.
/// </summary>
public class RedisRoomReadyStore(IConnectionMultiplexer connectionMultiplexer) : IRoomReadyStore
{
    private readonly IDatabase _database = connectionMultiplexer.GetDatabase();

    public async Task SetReadyAsync(long roomId, long userId, bool isReady, CancellationToken ct = default)
    {
        var key = RedisKeys.DungeonRoomReady(roomId);

        if (!isReady)
        {
            await _database.SetRemoveAsync(key, userId);
            return;
        }

        // 트랜잭션 내부 await 금지 — Task 를 버리고 ExecuteAsync 로 한 번에 실행한다.
        var transaction = _database.CreateTransaction();
        _ = transaction.SetAddAsync(key, userId);
        _ = transaction.KeyExpireAsync(key, RedisSettings.RedisCacheTtl);
        await transaction.ExecuteAsync();
    }

    public async Task<IReadOnlySet<long>> GetReadyUserIdsAsync(long roomId, CancellationToken ct = default)
    {
        var members = await _database.SetMembersAsync(RedisKeys.DungeonRoomReady(roomId));
        var result = new HashSet<long>(members.Length);
        foreach (var member in members)
        {
            if (long.TryParse(member.ToString(), out var userId))
                result.Add(userId);
        }

        return result;
    }

    public async Task<IReadOnlyDictionary<long, IReadOnlySet<long>>> GetReadyUserIdsAsync(
        IReadOnlyCollection<long> roomIds, CancellationToken ct = default)
    {
        if (roomIds.Count == 0)
            return new Dictionary<long, IReadOnlySet<long>>();

        // 명령을 먼저 다 던지고 한꺼번에 기다린다 — 멀티플렉서가 파이프라인으로 묶어
        // 방 개수만큼의 왕복이 사실상 1회로 줄어든다.
        var pending = roomIds
            .Distinct()
            .ToDictionary(roomId => roomId, roomId => _database.SetMembersAsync(RedisKeys.DungeonRoomReady(roomId)));

        await Task.WhenAll(pending.Values);

        var result = new Dictionary<long, IReadOnlySet<long>>(pending.Count);
        foreach (var (roomId, task) in pending)
        {
            var set = new HashSet<long>();
            foreach (var member in await task)
            {
                if (long.TryParse(member.ToString(), out var userId))
                    set.Add(userId);
            }

            result[roomId] = set;
        }

        return result;
    }

    public Task ClearAsync(long roomId, CancellationToken ct = default)
        => _database.KeyDeleteAsync(RedisKeys.DungeonRoomReady(roomId));
}
