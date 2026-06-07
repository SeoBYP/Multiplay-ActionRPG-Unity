using GameServer.Application.Domains.Inventory.Interfaces;
using GameServer.Domain.Entities.Inventory;

namespace GameServer.Tests.Infrastructure.Fakes.Repositories;

/// <summary>실제 InventoryRepository 의 적립/clamp 동작을 인메모리로 모사.</summary>
public class FakeInventoryRepository : IInventoryRepository
{
    private readonly Dictionary<(long UserId, string ItemId), InventoryItem> _items = new();

    public Task<List<InventoryItem>> GetAllAsync(long userId, CancellationToken ct = default)
        => Task.FromResult(_items.Values.Where(i => i.UserId == userId).ToList());

    public Task<InventoryItem> AddQuantityAsync(long userId, string itemId, int amount, int maxStack, CancellationToken ct = default)
    {
        if (_items.TryGetValue((userId, itemId), out var item))
        {
            var addable = Math.Max(0, maxStack - item.Quantity);
            item.Add(Math.Min(amount, addable));
        }
        else
        {
            item = InventoryItem.Create(userId, itemId, Math.Min(amount, maxStack));
            _items[(userId, itemId)] = item;
        }

        return Task.FromResult(item);
    }
}
