using GameServer.Application.Domains.Inventory.Interfaces;
using GameServer.Domain.Entities.Inventory;
using GameServer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace GameServer.Infrastructure.Domains.Inventory;

/// <summary>
/// 인벤토리 소유 저장소. Cache-Aside + Delete.
/// 캐시 = 유저당 Hash 1키(field=itemId, value=quantity). 빈 인벤토리는 캐시하지 않는다
/// (HGETALL 빈 결과 = MISS 와 구분 불가 → 빈 유저는 DB 폴백, 트래픽 낮아 무해).
/// </summary>
public class InventoryRepository(
    IConnectionMultiplexer connectionMultiplexer,
    GameServerDbContext context,
    ILogger<InventoryRepository> logger) : IInventoryRepository
{
    private readonly IDatabase _database = connectionMultiplexer.GetDatabase();

    public async Task<List<InventoryItem>> GetAllAsync(long userId, CancellationToken ct = default)
    {
        try
        {
            var entries = await _database.HashGetAllAsync(RedisKeys.UserInventory(userId));
            if (entries.Length > 0)
                return ParseFromRedis(userId, entries);

            // AsNoTracking: 오래 유지되는 DbContext 의 stale 방지
            var items = await context.InventoryItems
                .AsNoTracking()
                .Where(i => i.UserId == userId)
                .ToListAsync(ct);

            if (items.Count > 0)
                await SetCacheAsync(userId, items);

            return items;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get inventory for user {UserId}", userId);
            throw;
        }
    }

    public async Task<InventoryItem> AddQuantityAsync(long userId, int itemId, int amount, int maxStack, CancellationToken ct = default)
    {
        try
        {
            var item = await context.InventoryItems
                .SingleOrDefaultAsync(i => i.UserId == userId && i.ItemId == itemId, ct);

            if (item is null)
            {
                var initial = Math.Min(amount, maxStack);
                item = InventoryItem.Create(userId, itemId, initial);
                await context.InventoryItems.AddAsync(item, ct);
            }
            else
            {
                var addable = Math.Max(0, maxStack - item.Quantity);
                item.Add(Math.Min(amount, addable));
            }

            await context.SaveChangesAsync(ct);

            // 캐시는 덮어쓰지 않고 DEL (stale·부분갱신 방지)
            await DeleteCacheAsync(userId);

            return item;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to add item {ItemId} x{Amount} for user {UserId}", itemId, amount, userId);
            throw;
        }
    }

    public async Task<int?> RemoveQuantityAsync(long userId, int itemId, int amount, CancellationToken ct = default)
    {
        if (amount <= 0)
            return null;

        try
        {
            var item = await context.InventoryItems
                .SingleOrDefaultAsync(i => i.UserId == userId && i.ItemId == itemId, ct);

            if (item is null || amount > item.Quantity)
                return null;

            item.Remove(amount);
            if (item.Quantity == 0)
                context.InventoryItems.Remove(item);

            await context.SaveChangesAsync(ct);
            await DeleteCacheAsync(userId);

            return item.Quantity;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to remove item {ItemId} x{Amount} for user {UserId}", itemId, amount, userId);
            throw;
        }
    }

    private async Task SetCacheAsync(long userId, List<InventoryItem> items)
    {
        var key = RedisKeys.UserInventory(userId);
        var hashEntries = items
            .Select(i => new HashEntry(i.ItemId, i.Quantity))
            .ToArray();

        var transaction = _database.CreateTransaction();
        _ = transaction.HashSetAsync(key, hashEntries);
        _ = transaction.KeyExpireAsync(key, RedisSettings.RedisCacheTtl);

        bool committed = await transaction.ExecuteAsync();
        if (!committed)
            throw new InvalidOperationException("Failed to cache inventory: transaction rolled back");
    }

    private async Task DeleteCacheAsync(long userId)
    {
        var transaction = _database.CreateTransaction();
        _ = transaction.KeyDeleteAsync(RedisKeys.UserInventory(userId));
        bool committed = await transaction.ExecuteAsync();
        if (!committed)
            throw new InvalidOperationException("Failed to delete inventory cache");
    }

    private static List<InventoryItem> ParseFromRedis(long userId, HashEntry[] entries)
    {
        var items = new List<InventoryItem>(entries.Length);
        foreach (var entry in entries)
        {
            // field = numericId(int), value = 수량. 둘 다 파싱돼야 유효한 행이다.
            if (int.TryParse(entry.Name.ToString(), out var itemId) && itemId > 0
                && int.TryParse(entry.Value.ToString(), out var quantity) && quantity > 0)
                items.Add(InventoryItem.FromRedis(userId, itemId, quantity));
        }

        return items;
    }
}
