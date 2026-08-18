namespace Shared.Infrastructure.Items;

/// <summary>
/// 아이템 정의 카탈로그 — items.json(bake) 파사드. 소유 수량만 DB(InventoryItem)에 영속한다.
/// 공개 API 는 구 `GameServer.Domain.Entities.Inventory.ItemCatalog` 와 동일하다(호출부 무변경).
/// </summary>
public static class ItemCatalog
{
    /// <summary>정의가 존재하는 itemId 인지.</summary>
    public static bool Contains(string itemId) => ItemCatalogData.Current.ItemsById.ContainsKey(itemId);

    /// <summary>정의를 반환. 없으면 null.</summary>
    public static ItemDef? Get(string itemId) => ItemCatalogData.Current.ItemsById.GetValueOrDefault(itemId);

    /// <summary>전체 정의(저작 순서).</summary>
    public static IReadOnlyCollection<ItemDef> All => ItemCatalogData.Current.Items;
}
