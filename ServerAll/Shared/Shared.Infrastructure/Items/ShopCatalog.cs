namespace Shared.Infrastructure.Items;

/// <summary>
/// 상점 진열 카탈로그 — items.json(bake) 중 `isShopItem` 항목의 파사드. 가격은 서버 권위.
/// <see cref="All"/> 의 순서 = items.json 의 저작 순서 = **클라 상점 진열 순서**(정렬 금지).
/// 공개 API 는 구 `GameServer.Domain.Entities.Shop.ShopCatalog` 와 동일하다(호출부 무변경).
/// </summary>
public static class ShopCatalog
{
    /// <summary>상점에서 파는 itemId 인지.</summary>
    public static bool Contains(string itemId) => ItemCatalogData.Current.ShopById.ContainsKey(itemId);

    /// <summary>진열 정의를 반환. 안 파는 itemId 면 null.</summary>
    public static ShopItemDef? Get(string itemId) => ItemCatalogData.Current.ShopById.GetValueOrDefault(itemId);

    /// <summary>전체 진열(저작 순서 = 진열 순서).</summary>
    public static IReadOnlyCollection<ShopItemDef> All => ItemCatalogData.Current.Shop;
}
