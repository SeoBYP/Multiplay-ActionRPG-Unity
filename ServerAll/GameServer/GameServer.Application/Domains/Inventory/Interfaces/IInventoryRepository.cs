using GameServer.Domain.Entities.Inventory;

namespace GameServer.Application.Domains.Inventory.Interfaces;

/// <summary>
/// 인벤토리 소유 영속 저장소. Cache-Aside + Delete 패턴(networking.md).
/// 정의(ItemCatalog)는 다루지 않는다 — 수량(InventoryItem)만 영속.
/// </summary>
public interface IInventoryRepository
{
    /// <summary>유저의 전체 인벤토리를 읽는다(캐시 우선). 빈 인벤토리면 빈 리스트.</summary>
    Task<List<InventoryItem>> GetAllAsync(long userId, CancellationToken ct = default);

    /// <summary>
    /// (userId, itemId) 수량을 누적 적립한다. 행이 없으면 생성(lazy), 있으면 += amount.
    /// maxStack 으로 상한 clamp. DB 저장 후 유저 인벤토리 캐시 DEL. 갱신된 소유를 반환.
    /// </summary>
    Task<InventoryItem> AddQuantityAsync(long userId, int itemId, int amount, int maxStack, CancellationToken ct = default);

    /// <summary>
    /// (userId, itemId) 수량을 차감한다. 미보유이거나 보유보다 많이 차감하면 null(변화 없음).
    /// 차감 후 0 이 되면 행을 삭제한다. DB 저장 후 캐시 DEL. 성공 시 남은 수량(0 가능)을 반환.
    /// </summary>
    Task<int?> RemoveQuantityAsync(long userId, int itemId, int amount, CancellationToken ct = default);
}
