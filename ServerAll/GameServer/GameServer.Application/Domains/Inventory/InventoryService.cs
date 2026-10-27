using GameServer.Application.Domains.Codex.Interfaces;
using GameServer.Application.Domains.Inventory.Interfaces;
using GameServer.Domain.Entities.Inventory;
using Shared.Infrastructure.Items;

namespace GameServer.Application.Domains.Inventory;

/// <summary>
/// 인벤토리 서비스 구현. 정의 검증(ItemCatalog) 후 수량 적립을 저장소에 위임한다.
/// 멱등/보상 산정은 호출자 책임 — 여기선 단건 지급만. 지급은 모든 획득 경로의 단일 funnel 이라 도감 발견도 여기서 기록한다.
/// </summary>
public sealed class InventoryService(IInventoryRepository repository, ICodexService codex) : IInventoryService
{
    public Task<List<InventoryItem>> GetInventoryAsync(long userId, CancellationToken ct = default)
        => repository.GetAllAsync(userId, ct);

    public async Task<ItemGrantResult> GrantItemAsync(long userId, int itemId, int amount, CancellationToken ct = default)
    {
        if (amount <= 0)
            return ItemGrantResult.Fail(itemId, "amount must be positive");

        var def = ItemCatalog.Get(itemId);
        if (def is null)
            return ItemGrantResult.Fail(itemId, "unknown item");

        var item = await repository.AddQuantityAsync(userId, itemId, amount, def.MaxStack, ct);

        await codex.MarkDiscoveredAsync(userId, itemId, ct);

        return ItemGrantResult.Ok(itemId, item.Quantity);
    }

    public async Task<ItemConsumeResult> ConsumeItemAsync(long userId, int itemId, int amount, CancellationToken ct = default)
    {
        if (amount <= 0)
            return ItemConsumeResult.Fail(itemId, "amount must be positive");

        // 보유 검증은 저장소가 원자적으로 수행(미보유/부족 → null)
        var remaining = await repository.RemoveQuantityAsync(userId, itemId, amount, ct);
        if (remaining is null)
            return ItemConsumeResult.Fail(itemId, "not owned or insufficient quantity");

        return ItemConsumeResult.Ok(itemId, remaining.Value);
    }
}
