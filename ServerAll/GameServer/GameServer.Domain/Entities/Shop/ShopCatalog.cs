namespace GameServer.Domain.Entities.Shop;

/// <summary>
/// 상점 진열 카탈로그 — 코드 시드(정적 기획데이터). DB 테이블 아님.
/// ItemCatalog·EquipmentCatalog 와 동일 컨벤션: 정적 기획데이터는 카탈로그로.
/// 가격은 서버 권위(클라가 못 정함). 여기 등록된 itemId 는 ItemCatalog 에도 있어야 한다(지급 경로).
/// 스탯 미리보기는 EquipmentCatalog 에서 파생한다(중복 저작 금지).
/// </summary>
public static class ShopCatalog
{
    private static readonly Dictionary<string, ShopItemDef> Items = new()
    {
        // 소모품
        ["potion_hp_small"]   = new ShopItemDef("potion_hp_small", BuyPrice: 50, SellPrice: 10, ShopCategory.Potion),
        ["potion_mp_small"]   = new ShopItemDef("potion_mp_small", BuyPrice: 50, SellPrice: 10, ShopCategory.Potion),

        // 무기
        ["sword_basic"]       = new ShopItemDef("sword_basic", BuyPrice: 200, SellPrice: 50, ShopCategory.Weapon),

        // 방어구(투구/갑옷/장갑/신발/방패)
        ["armor_leather"]     = new ShopItemDef("armor_leather", BuyPrice: 150, SellPrice: 40, ShopCategory.Armor),
        ["helmet_iron"]       = new ShopItemDef("helmet_iron", BuyPrice: 120, SellPrice: 30, ShopCategory.Armor),
        ["boots_leather"]     = new ShopItemDef("boots_leather", BuyPrice: 100, SellPrice: 25, ShopCategory.Armor),
        ["gloves_leather"]    = new ShopItemDef("gloves_leather", BuyPrice: 100, SellPrice: 25, ShopCategory.Armor),
        ["shield_wooden"]     = new ShopItemDef("shield_wooden", BuyPrice: 130, SellPrice: 35, ShopCategory.Armor),

        // 장신구(반지/목걸이)
        ["ring_power"]        = new ShopItemDef("ring_power", BuyPrice: 300, SellPrice: 80, ShopCategory.Accessory),
        ["necklace_vitality"] = new ShopItemDef("necklace_vitality", BuyPrice: 300, SellPrice: 80, ShopCategory.Accessory),
    };

    /// <summary>상점에서 파는 itemId 인지.</summary>
    public static bool Contains(string itemId) => Items.ContainsKey(itemId);

    /// <summary>진열 정의를 반환. 안 파는 itemId 면 null.</summary>
    public static ShopItemDef? Get(string itemId) => Items.GetValueOrDefault(itemId);

    /// <summary>전체 진열(조회용).</summary>
    public static IReadOnlyCollection<ShopItemDef> All => Items.Values;
}
