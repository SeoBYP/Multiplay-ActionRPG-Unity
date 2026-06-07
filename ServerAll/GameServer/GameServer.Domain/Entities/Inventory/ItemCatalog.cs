namespace GameServer.Domain.Entities.Inventory;

/// <summary>
/// 아이템 정의 카탈로그 — 코드 시드(정적 기획데이터). DB 테이블 아님.
/// 프로젝트 컨벤션(GameplayEffectCatalog·MonsterCatalog·spawn-layouts)과 동일: 정적데이터는 카탈로그로.
/// 소유 수량만 DB(InventoryItem)에 영속한다.
/// </summary>
public static class ItemCatalog
{
    private static readonly Dictionary<string, ItemDef> Items = new()
    {
        ["potion_hp_small"] = new ItemDef("potion_hp_small", "소형 체력 물약", ItemGrade.Common, Stackable: true, MaxStack: 99, "icon_potion_hp_s"),
        ["potion_mp_small"] = new ItemDef("potion_mp_small", "소형 마나 물약", ItemGrade.Common, Stackable: true, MaxStack: 99, "icon_potion_mp_s"),
        ["gold_pouch"]      = new ItemDef("gold_pouch", "골드 주머니", ItemGrade.Rare, Stackable: true, MaxStack: 999, "icon_gold_pouch"),
    };

    /// <summary>정의가 존재하는 itemId 인지.</summary>
    public static bool Contains(string itemId) => Items.ContainsKey(itemId);

    /// <summary>정의를 반환. 없으면 null.</summary>
    public static ItemDef? Get(string itemId) => Items.GetValueOrDefault(itemId);

    /// <summary>전체 정의(조회/디버그용).</summary>
    public static IReadOnlyCollection<ItemDef> All => Items.Values;
}
