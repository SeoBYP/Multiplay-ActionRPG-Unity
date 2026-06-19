namespace GameServer.Domain.Entities.Shop;

/// <summary>
/// 상점 진열 한 항목(정적 기획데이터). 가격은 서버 권위 — 클라가 가격을 정하지 못한다.
/// 같은 itemId 로 ItemCatalog(소유 메타)·EquipmentCatalog(장비 스탯)와 묶인다.
/// 스탯 미리보기는 EquipmentCatalog 에서 파생(중복 저작 금지) — 여기엔 가격·분류만.
/// </summary>
/// <param name="ItemId">카탈로그 키. ItemCatalog/InventoryItem 과 동일 식별자.</param>
/// <param name="BuyPrice">구매가(골드). 양수.</param>
/// <param name="SellPrice">판매가(골드). 아이템별 명시(보통 BuyPrice 미만).</param>
/// <param name="Category">진열 분류(클라 탭).</param>
public sealed record ShopItemDef(
    string ItemId,
    long BuyPrice,
    long SellPrice,
    ShopCategory Category);
