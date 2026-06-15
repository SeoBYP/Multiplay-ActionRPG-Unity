using Shared.Gameplay.Equipment;

namespace GameServer.Domain.Entities.Equipment;

/// <summary>
/// 장비 정의 카탈로그 — 코드 시드(정적 기획데이터). DB 테이블 아님.
/// ItemCatalog·GameplayEffectCatalog 와 동일 컨벤션: 정적 기획데이터는 카탈로그로.
/// 착용 상태(UserId,Slot→ItemId)만 DB(user_equipments)에 영속한다.
/// 여기 등록된 itemId 는 ItemCatalog 에도 Stackable:false 로 등록돼 있어야 한다(소유 경로).
/// </summary>
public static class EquipmentCatalog
{
    private static readonly Dictionary<string, EquipmentDef> Items = new()
    {
        ["sword_basic"]       = new EquipmentDef("sword_basic", EquipmentType.Weapon, new EquipmentStatModifier(AttackPower: 5)),
        ["armor_leather"]     = new EquipmentDef("armor_leather", EquipmentType.Armor, new EquipmentStatModifier(Defense: 3)),
        ["helmet_iron"]       = new EquipmentDef("helmet_iron", EquipmentType.Header, new EquipmentStatModifier(Defense: 2, MaxHealth: 5)),
        ["boots_leather"]     = new EquipmentDef("boots_leather", EquipmentType.Shoose, new EquipmentStatModifier(Defense: 1, Dexterity: 2)),
        ["gloves_leather"]    = new EquipmentDef("gloves_leather", EquipmentType.Glove, new EquipmentStatModifier(AttackPower: 2)),
        ["shield_wooden"]     = new EquipmentDef("shield_wooden", EquipmentType.Shield, new EquipmentStatModifier(Defense: 4)),
        ["ring_power"]        = new EquipmentDef("ring_power", EquipmentType.Ring, new EquipmentStatModifier(AttackPower: 3, Strength: 2)),
        ["necklace_vitality"] = new EquipmentDef("necklace_vitality", EquipmentType.Necklace, new EquipmentStatModifier(MaxHealth: 20)),
    };

    /// <summary>장비 정의가 존재하는 itemId 인지(= 장착 가능한가).</summary>
    public static bool IsEquippable(string itemId) => Items.ContainsKey(itemId);

    /// <summary>장비 정의를 반환. 장비가 아니면 null.</summary>
    public static EquipmentDef? Get(string itemId) => Items.GetValueOrDefault(itemId);

    /// <summary>전체 정의(조회/디버그용).</summary>
    public static IReadOnlyCollection<EquipmentDef> All => Items.Values;
}
