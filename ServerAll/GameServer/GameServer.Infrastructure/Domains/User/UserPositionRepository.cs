using GameServer.Application.Domains.User.Interfaces;
using GameServer.Domain.Entities.User;
using GameServer.Infrastructure.Domains;
using GameServer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace GameServer.Infrastructure.Domains.User;

/// <summary>
/// Main 위치 저장소 — **Redis 가 1차, DB 가 확정**.
///
/// ⚠ 이 도메인만 cache-aside 교리의 예외다(networking.md 에 사유 기재).
/// 다른 저장소는 "DB 저장 → 캐시 DEL" 인데, 위치는 주기 보고라 쓰기가 매우 잦고 유실이 허용된다.
/// 그래서 주기 쓰기는 Redis 로만 가고, 이탈 시점(로그아웃·던전 입장)에 한 번 DB 로 확정한다.
/// 유실 폭 = "마지막 확정 이후" 로 한정되고, 그때는 마지막 정상 이탈 위치로 되돌아간다.
/// </summary>
public sealed class UserPositionRepository(
    IConnectionMultiplexer connectionMultiplexer,
    GameServerDbContext context,
    ILogger<UserPositionRepository> logger) : IUserPositionRepository
{
    private readonly IDatabase _redis = connectionMultiplexer.GetDatabase();

    /// <summary>휘발 수명 — 이보다 오래 안 들어오면 DB 확정값으로 돌아간다.</summary>
    private static readonly TimeSpan VolatileTtl = TimeSpan.FromHours(24);

    public async Task SaveVolatileAsync(UserPosition position, CancellationToken ct = default)
    {
        var key = RedisKeys.UserPosition(position.UserId);

        var tx = _redis.CreateTransaction();
        _ = tx.HashSetAsync(key,
        [
            new HashEntry("mapId", position.MapId),
            new HashEntry("x", position.X),
            new HashEntry("y", position.Y),
            new HashEntry("z", position.Z),
            new HashEntry("rotY", position.RotY),
        ]);
        _ = tx.KeyExpireAsync(key, VolatileTtl);
        await tx.ExecuteAsync();
    }

    public async Task<UserPosition?> GetAsync(long userId, CancellationToken ct = default)
    {
        var cached = await ReadVolatileAsync(userId);
        if (cached is not null)
            return cached;

        // 캐시 미스 → DB. 읽기 전용이므로 추적 불필요(AsNoTracking — stale 엔티티 반환 방지).
        return await context.UserPositions
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.UserId == userId, ct);
    }

    public async Task FlushToDatabaseAsync(long userId, CancellationToken ct = default)
    {
        var latest = await ReadVolatileAsync(userId);
        if (latest is null)
            return; // 이번 세션에 보고가 없었다 — 기존 확정값을 그대로 둔다.

        var row = await context.UserPositions.SingleOrDefaultAsync(p => p.UserId == userId, ct);
        if (row is null)
        {
            await context.UserPositions.AddAsync(
                UserPosition.Create(userId, latest.MapId, latest.X, latest.Y, latest.Z, latest.RotY), ct);
        }
        else
        {
            row.Update(latest.MapId, latest.X, latest.Y, latest.Z, latest.RotY);
        }

        await context.SaveChangesAsync(ct);
        logger.LogInformation("[Position] 확정 저장 user={UserId} map={MapId}", userId, latest.MapId);
    }

    private async Task<UserPosition?> ReadVolatileAsync(long userId)
    {
        var entries = await _redis.HashGetAllAsync(RedisKeys.UserPosition(userId));
        if (entries.Length == 0)
            return null;

        var map = entries.ToDictionary(e => e.Name.ToString(), e => e.Value);
        var mapId = map.GetValueOrDefault("mapId").ToString();
        if (string.IsNullOrEmpty(mapId))
            return null;

        return UserPosition.FromRedis(
            userId,
            mapId,
            (float)map.GetValueOrDefault("x", 0d),
            (float)map.GetValueOrDefault("y", 0d),
            (float)map.GetValueOrDefault("z", 0d),
            (float)map.GetValueOrDefault("rotY", 0d));
    }
}
