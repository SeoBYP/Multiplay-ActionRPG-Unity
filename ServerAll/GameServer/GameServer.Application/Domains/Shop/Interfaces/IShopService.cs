using GameServer.Domain.Entities.Shop;

namespace GameServer.Application.Domains.Shop.Interfaces;

/// <summary>
/// 상점 도메인 서비스. 진열 조회 + 구매/판매(서버 권위). 가격은 ShopCatalog(서버)만 안다.
/// 구매=지갑 차감→인벤토리 지급, 판매=인벤토리 차감→지갑 적립. 영속은 Wallet/Inventory 가 소유(상점은 조합만).
/// </summary>
public interface IShopService
{
    /// <summary>상점 진열 전체(정적 카탈로그). 가격·분류 포함.</summary>
    IReadOnlyCollection<ShopItemDef> GetItems();

    /// <summary>구매: 골드 차감(부족 시 거부) → 아이템 지급. 지급 실패 시 환불. 안 파는 itemId·qty≤0 거부.</summary>
    Task<ShopBuyResult> BuyAsync(long userId, string itemId, int qty, CancellationToken ct = default);

    /// <summary>판매: 아이템 차감(미보유/부족 시 거부) → 골드 적립. 안 파는 itemId·qty≤0 거부.</summary>
    Task<ShopSellResult> SellAsync(long userId, string itemId, int qty, CancellationToken ct = default);
}
