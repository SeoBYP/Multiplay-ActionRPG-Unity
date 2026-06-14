using System.Globalization;
using GameServer.Application.Domains.Progression.Interfaces;
using GameServer.Domain.Entities.User;
using GameServer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace GameServer.Infrastructure.Domains.Progression;

/// <summary>
/// 진행(레벨·경험치) 저장소. Cache-Aside + Delete (networking.md 규칙).
///   Get    : Redis HIT 즉시 반환 / MISS → DB(AsNoTracking) → 캐시 SET(TTL)
///   AddExp : DB 적립(없으면 lazy create) → SaveChanges → 캐시 DEL (다음 Get 이 재캐싱)
/// </summary>
public class ProgressionRepository(
    IConnectionMultiplexer connectionMultiplexer,
    GameServerDbContext context,
    ILogger<ProgressionRepository> logger) : IProgressionRepository
{
    private readonly IDatabase _database = connectionMultiplexer.GetDatabase();

    public async Task<UserProgression?> GetAsync(long userId, CancellationToken ct = default)
    {
        try
        {
            var entries = await _database.HashGetAllAsync(RedisKeys.UserProgression(userId));
            if (entries.Length > 0)
                return ParseFromRedis(userId, entries);

            // cache-aside 읽기 전용 → AsNoTracking (long-lived DbContext stale 방지)
            var progression = await context.UserProgressions
                .AsNoTracking()
                .SingleOrDefaultAsync(p => p.UserId == userId, ct);
            if (progression is null)
                return null;

            await SetCacheAsync(progression);
            return progression;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get progression for user {UserId}", userId);
            throw;
        }
    }

    public async Task<UserProgression> AddExpAsync(long userId, long amount, CancellationToken ct = default)
    {
        try
        {
            // 쓰기 경로 — 추적 쿼리로 로드(없으면 lazy create) 후 적립.
            var progression = await context.UserProgressions
                .SingleOrDefaultAsync(p => p.UserId == userId, ct);

            if (progression is null)
            {
                progression = UserProgression.Create(userId);
                progression.AddExp(amount, LevelTableCurve.Instance);
                await context.UserProgressions.AddAsync(progression, ct);
            }
            else
            {
                progression.AddExp(amount, LevelTableCurve.Instance);
            }

            await context.SaveChangesAsync(ct);

            // Update 는 캐시를 덮어쓰지 않고 DEL (stale·부분갱신 위험 회피).
            await DeleteCacheAsync(userId);

            return progression;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to add exp for user {UserId}", userId);
            throw;
        }
    }

    private async Task SetCacheAsync(UserProgression p)
    {
        var transaction = _database.CreateTransaction();

        _ = transaction.HashSetAsync(RedisKeys.UserProgression(p.UserId),
        [
            new HashEntry("UserId", p.UserId),
            new HashEntry("Level", p.Level),
            new HashEntry("Exp", p.Exp),
            new HashEntry("UpdatedAt", p.UpdatedAt.ToString("O", CultureInfo.InvariantCulture)),
        ]);
        _ = transaction.KeyExpireAsync(RedisKeys.UserProgression(p.UserId), RedisSettings.RedisCacheTtl);

        bool committed = await transaction.ExecuteAsync();
        if (!committed)
            throw new InvalidOperationException("Failed to cache progression: transaction rolled back");
    }

    private async Task DeleteCacheAsync(long userId)
    {
        var transaction = _database.CreateTransaction();
        _ = transaction.KeyDeleteAsync(RedisKeys.UserProgression(userId));
        bool committed = await transaction.ExecuteAsync();
        if (!committed)
            throw new InvalidOperationException("Failed to delete progression cache");
    }

    private UserProgression? ParseFromRedis(long userId, HashEntry[] entries)
    {
        var dict = entries.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());

        if (!dict.TryGetValue("Level", out var levelStr) ||
            !dict.TryGetValue("Exp", out var expStr) ||
            !dict.TryGetValue("UpdatedAt", out var updatedAtStr))
        {
            logger.LogWarning("Progression {UserId} has missing fields", userId);
            return null;
        }

        if (!int.TryParse(levelStr, out var level) ||
            !long.TryParse(expStr, out var exp) ||
            !DateTime.TryParse(updatedAtStr, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var updatedAt))
        {
            return null;
        }

        return UserProgression.FromRedis(userId, level, exp, updatedAt);
    }
}
