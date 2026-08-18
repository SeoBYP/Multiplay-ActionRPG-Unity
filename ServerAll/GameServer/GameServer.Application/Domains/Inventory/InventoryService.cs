using GameServer.Application.Domains.Codex.Interfaces;
using GameServer.Application.Domains.Inventory.Interfaces;
using GameServer.Domain.Entities.Inventory;
using Shared.Infrastructure.Items;

namespace GameServer.Application.Domains.Inventory;

/// <summary>
/// 인벤토리 서비스 구현. 정의 검증(ItemCatalog) 후 저장소에 수량 적립을 위임한다.
/// 멱등/보상 산정은 호출자(3.3 루트 등) 책임 — 여기선 단건 지급만.
///
/// 도감(3.7): 지급은 모든 획득 경로(루트·상점·ClaimKill)의 단일 funnel 이므로, 여기서 발견을 기록한다(서버 권위).
/// </summary>
public sealed class InventoryService(IInventoryRepository repository, ICodexService codex) : IInventoryService
{
    public Task<List<InventoryItem>> GetInventoryAsync(long userId, CancellationToken ct = default)
        => repository.GetAllAsync(userId, ct);

    public async Task<ItemGrantResult> GrantItemAsync(long userId, string itemId, int amount, CancellationToken ct = default)
    {
        if (amount <= 0)
            return ItemGrantResult.Fail(itemId, "amount must be positive");

        var def = ItemCatalog.Get(itemId);
        if (def is null)
            return ItemGrantResult.Fail(itemId, "unknown item");

        var item = await repository.AddQuantityAsync(userId, itemId, amount, def.MaxStack, ct);

        // 도감 발견 기록(멱등). 단일 획득 funnel 이라 모든 경로의 첫 획득이 여기서 도감에 남는다.
        await codex.MarkDiscoveredAsync(userId, itemId, ct);

        return ItemGrantResult.Ok(itemId, item.Quantity);
    }

    public async Task<ItemConsumeResult> ConsumeItemAsync(long userId, string itemId, int amount, CancellationToken ct = default)
    {
        if (amount <= 0)
            return ItemConsumeResult.Fail(itemId, "amount must be positive");

        // 소비는 보유 검증을 저장소가 원자적으로 수행(미보유/부족 → null). 카탈로그 검증 불필요 — 보유한 것만 소비 가능.
        var remaining = await repository.RemoveQuantityAsync(userId, itemId, amount, ct);
        if (remaining is null)
            return ItemConsumeResult.Fail(itemId, "not owned or insufficient quantity");

        return ItemConsumeResult.Ok(itemId, remaining.Value);
    }
}
