using GameServer.Domain.Entities.Inventory;

namespace GameServer.Application.Domains.Inventory.Interfaces;

/// <summary>
/// 인벤토리 도메인 서비스. 조회 + 지급(보상/루트 진입점).
/// 아이템 정의 검증(ItemCatalog)은 여기서 — 저장소는 수량만 다룬다.
/// </summary>
public interface IInventoryService
{
    /// <summary>유저의 전체 인벤토리.</summary>
    Task<List<InventoryItem>> GetInventoryAsync(long userId, CancellationToken ct = default);

    /// <summary>아이템을 지급(적립). itemId 가 카탈로그에 없거나 amount≤0 이면 실패.</summary>
    Task<ItemGrantResult> GrantItemAsync(long userId, int itemId, int amount, CancellationToken ct = default);

    /// <summary>아이템을 소비(차감). 미보유·보유부족·amount≤0 이면 실패. 성공 시 남은 수량 반환.</summary>
    Task<ItemConsumeResult> ConsumeItemAsync(long userId, int itemId, int amount, CancellationToken ct = default);
}
