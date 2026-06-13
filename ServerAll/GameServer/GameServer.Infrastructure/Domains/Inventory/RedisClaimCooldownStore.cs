using GameServer.Application.Domains.Inventory.Interfaces;
using StackExchange.Redis;

namespace GameServer.Infrastructure.Domains.Inventory;

/// <summary>
/// Main 킬 클레임 쿨다운 저장소 — Redis `SET NX PX` 구현.
/// 키 없음 → 점유 성공(true)+TTL 자동만료 / 키 존재 → 쿨다운 중(false). 원자적, 레이스 없음.
/// </summary>
public sealed class RedisClaimCooldownStore : IClaimCooldownStore
{
    private readonly IDatabase _redis;

    public RedisClaimCooldownStore(IConnectionMultiplexer connectionMultiplexer)
        => _redis = connectionMultiplexer.GetDatabase();

    public Task<bool> TryClaimAsync(string key, TimeSpan ttl, CancellationToken ct = default)
        => _redis.StringSetAsync(key, "1", ttl, when: When.NotExists);
}
