using GameServer.Application.Domains.Codex.Interfaces;
using GameServer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameServer.Infrastructure.Domains.Codex;

/// <summary>
/// 도감 발견 기록 저장소. DB 전용(Redis 캐시 없음) — write-once·read-rare(도감 열 때만)라 캐시 이득 낮음.
///   Get : DB(AsNoTracking) — long-lived DbContext stale 방지(networking.md).
///   Add : INSERT ... ON CONFLICT DO NOTHING (멱등). 동시 첫 획득 경합에도 안전(복합 PK).
/// </summary>
public class CodexRepository(
    GameServerDbContext context,
    ILogger<CodexRepository> logger) : ICodexRepository
{
    public async Task<List<string>> GetDiscoveredItemIdsAsync(long userId, CancellationToken ct = default)
    {
        try
        {
            return await context.UserCodexEntries
                .AsNoTracking()
                .Where(e => e.UserId == userId)
                .Select(e => e.ItemId)
                .ToListAsync(ct);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get codex for user {UserId}", userId);
            throw;
        }
    }

    public async Task<bool> AddDiscoveredAsync(long userId, string itemId, CancellationToken ct = default)
    {
        try
        {
            // ON CONFLICT DO NOTHING — 이미 발견했으면 0행(멱등). EF 추적/식별맵 우회(raw SQL)라 동시 경합도 안전.
            var rows = await context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 INSERT INTO user_codex ("UserId", "ItemId", "DiscoveredAt")
                 VALUES ({userId}, {itemId}, {DateTime.UtcNow})
                 ON CONFLICT ("UserId", "ItemId") DO NOTHING
                 """, ct);

            return rows > 0;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to mark codex discovery {ItemId} for user {UserId}", itemId, userId);
            throw;
        }
    }
}
