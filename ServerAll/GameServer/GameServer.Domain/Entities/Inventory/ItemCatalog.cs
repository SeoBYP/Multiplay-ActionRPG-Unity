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
        // 골드(Currencies.Gold)는 더 이상 인벤토리 아이템이 아니다 — 통화(UserWallet 잔액)로 적립(3.4).

        // 장비(3.2) — 개별 인스턴스 없이 스택형 소유(qty=1). 슬롯·스탯 정의는 EquipmentCatalog. 8슬롯 1종씩.
        ["sword_basic"]       = new ItemDef("sword_basic", "기본 검", ItemGrade.Common, Stackable: false, MaxStack: 1, "icon_sword_basic"),
        ["armor_leather"]     = new ItemDef("armor_leather", "가죽 갑옷", ItemGrade.Common, Stackable: false, MaxStack: 1, "icon_armor_leather"),
        ["helmet_iron"]       = new ItemDef("helmet_iron", "철 투구", ItemGrade.Common, Stackable: false, MaxStack: 1, "icon_helmet_iron"),
        ["boots_leather"]     = new ItemDef("boots_leather", "가죽 부츠", ItemGrade.Common, Stackable: false, MaxStack: 1, "icon_boots_leather"),
        ["gloves_leather"]    = new ItemDef("gloves_leather", "가죽 장갑", ItemGrade.Common, Stackable: false, MaxStack: 1, "icon_gloves_leather"),
        ["shield_wooden"]     = new ItemDef("shield_wooden", "나무 방패", ItemGrade.Common, Stackable: false, MaxStack: 1, "icon_shield_wooden"),
        ["ring_power"]        = new ItemDef("ring_power", "힘의 반지", ItemGrade.Rare, Stackable: false, MaxStack: 1, "icon_ring_power"),
        ["necklace_vitality"] = new ItemDef("necklace_vitality", "활력의 목걸이", ItemGrade.Rare, Stackable: false, MaxStack: 1, "icon_necklace_vitality"),
    };

    /// <summary>정의가 존재하는 itemId 인지.</summary>
    public static bool Contains(string itemId) => Items.ContainsKey(itemId);

    /// <summary>정의를 반환. 없으면 null.</summary>
    public static ItemDef? Get(string itemId) => Items.GetValueOrDefault(itemId);

    /// <summary>전체 정의(조회/디버그용).</summary>
    public static IReadOnlyCollection<ItemDef> All => Items.Values;
}
