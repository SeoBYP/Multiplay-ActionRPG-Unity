using GameServer.Application.Domains.Equipment.Interfaces;
using GameServer.Domain.Entities.Equipment;
using GameServer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Gameplay.Equipment;
using StackExchange.Redis;

namespace GameServer.Infrastructure.Domains.Equipment;

/// <summary>
/// 착용 상태 저장소. Cache-Aside + Delete (networking.md).
///   GetEquipped : Redis Hash HIT 즉시 반환 / MISS → DB(AsNoTracking) → 캐시 SET(TTL)
///   Set/Clear   : DB upsert/삭제 → SaveChanges → 캐시 DEL
///
/// 캐시 = 유저당 Hash 1키(field=(int)slot, value=itemId). 빈 착용은 캐시하지 않는다
/// (HGETALL 빈 결과 = MISS 와 구분 불가 → 빈 유저는 DB 폴백, 트래픽 낮아 무해 — 인벤토리와 동일).
/// </summary>
public class EquipmentRepository(
    IConnectionMultiplexer connectionMultiplexer,
    GameServerDbContext context,
    ILogger<EquipmentRepository> logger) : IEquipmentRepository
{
    private readonly IDatabase _database = connectionMultiplexer.GetDatabase();

    public async Task<List<UserEquipment>> GetEquippedAsync(long userId, CancellationToken ct = default)
    {
        try
        {
            var entries = await _database.HashGetAllAsync(RedisKeys.UserEquipment(userId));
            if (entries.Length > 0)
                return ParseFromRedis(userId, entries);

            // cache-aside 읽기 전용 → AsNoTracking (long-lived DbContext stale 방지)
            var equipped = await context.UserEquipments
                .AsNoTracking()
                .Where(e => e.UserId == userId)
                .ToListAsync(ct);

            if (equipped.Count > 0)
                await SetCacheAsync(userId, equipped);

            return equipped;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get equipment for user {UserId}", userId);
            throw;
        }
    }

    public async Task SetAsync(long userId, EquipmentType slot, int itemId, CancellationToken ct = default)
    {
        try
        {
            // 쓰기 경로 — 추적 쿼리로 로드(없으면 신규, 있으면 itemId 교체).
            var existing = await context.UserEquipments
                .SingleOrDefaultAsync(e => e.UserId == userId && e.Slot == slot, ct);

            if (existing is null)
                await context.UserEquipments.AddAsync(UserEquipment.Create(userId, slot, itemId), ct);
            else
                existing.ChangeItem(itemId);

            await context.SaveChangesAsync(ct);
            await DeleteCacheAsync(userId);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to set equipment {Slot}={ItemId} for user {UserId}", slot, itemId, userId);
            throw;
        }
    }

    public async Task<bool> ClearAsync(long userId, EquipmentType slot, CancellationToken ct = default)
    {
        try
        {
            var existing = await context.UserEquipments
                .SingleOrDefaultAsync(e => e.UserId == userId && e.Slot == slot, ct);

            if (existing is null)
                return false; // 비어 있던 슬롯 (멱등 — 서비스가 성공 처리)

            context.UserEquipments.Remove(existing);
            await context.SaveChangesAsync(ct);
            await DeleteCacheAsync(userId);
            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to clear equipment {Slot} for user {UserId}", slot, userId);
            throw;
        }
    }

    private async Task SetCacheAsync(long userId, List<UserEquipment> equipped)
    {
        var key = RedisKeys.UserEquipment(userId);
        var hashEntries = equipped
            .Select(e => new HashEntry((int)e.Slot, e.ItemId))
            .ToArray();

        var transaction = _database.CreateTransaction();
        _ = transaction.HashSetAsync(key, hashEntries);
        _ = transaction.KeyExpireAsync(key, RedisSettings.RedisCacheTtl);

        bool committed = await transaction.ExecuteAsync();
        if (!committed)
            throw new InvalidOperationException("Failed to cache equipment: transaction rolled back");
    }

    private async Task DeleteCacheAsync(long userId)
    {
        var transaction = _database.CreateTransaction();
        _ = transaction.KeyDeleteAsync(RedisKeys.UserEquipment(userId));
        bool committed = await transaction.ExecuteAsync();
        if (!committed)
            throw new InvalidOperationException("Failed to delete equipment cache");
    }

    private static List<UserEquipment> ParseFromRedis(long userId, HashEntry[] entries)
    {
        var equipped = new List<UserEquipment>(entries.Length);
        foreach (var entry in entries)
        {
            if (int.TryParse(entry.Name.ToString(), out var slotValue)
                && Enum.IsDefined(typeof(EquipmentType), slotValue))
            {
                // 캐시 값은 numericId(int). 파싱 실패/0 은 빈 슬롯으로 보고 건너뛴다(문자열 시절 빈 문자열의 자리).
                if (int.TryParse(entry.Value.ToString(), out var itemId) && itemId > 0)
                    equipped.Add(UserEquipment.FromRedis(userId, (EquipmentType)slotValue, itemId));
            }
        }

        return equipped;
    }
}
