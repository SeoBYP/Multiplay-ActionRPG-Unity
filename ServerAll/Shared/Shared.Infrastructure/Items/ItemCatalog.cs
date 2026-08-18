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

    /// <summary>정의가 존재하는 numericId 인지.</summary>
    public static bool Contains(int numericId) => ItemCatalogData.Current.ItemsByNumericId.ContainsKey(numericId);

    /// <summary>numericId 로 아이템 정의를 반환. 없으면 null.
    /// <para>서버 내부는 numericId(int)가 키다 — 문자열 조회는 proto·로그 경계에만 남는다.</para></summary>
    public static ItemDef? Get(int numericId) => ItemCatalogData.Current.ItemsByNumericId.GetValueOrDefault(numericId);

    /// <summary>전체 정의(저작 순서).</summary>
    public static IReadOnlyCollection<ItemDef> All => ItemCatalogData.Current.Items;
}
