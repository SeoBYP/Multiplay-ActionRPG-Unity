using Shared.Gameplay.Equipment;
using Shared.Gameplay.Items;

namespace Shared.Infrastructure.Items;

/// <summary>
/// 아이템 *정의*(정적 기획데이터) — 소유(InventoryItem)와 분리. 정의는 DB 가 아니라 bake 카탈로그.
/// 표시 필드(이름·설명·아이콘·등급·분류)는 **서버가 쓰지 않으므로 bake 하지 않는다** — 클라 SO 전용.
/// </summary>
/// <param name="ItemId">카탈로그 키(예 "potion_hp_small"). InventoryItem·proto 가 참조하는 식별자.</param>
/// <param name="Stackable">스택 가능 여부. false 면 MaxStack=1 취급.</param>
/// <param name="MaxStack">한 칸 최대 수량.</param>
public sealed record ItemDef(
    string ItemId,
    bool Stackable,
    int MaxStack);

/// <summary>
/// 장비 한 점이 더하는 **가산 스탯 모디파이어**(서버 권위). PlayerStats 의 합산 가능한 항목과 1:1.
/// 합산은 GetStatsAsync 단일 권위에서 base(레벨 룩업) 위에 Σ 한다(authority-model §4c). 기본값 0 = 영향 없음.
/// </summary>
public readonly record struct EquipmentStatModifier(
    int MaxHealth = 0,
    int MaxMana = 0,
    int AttackPower = 0,
    int Defense = 0,
    int Strength = 0,
    int Dexterity = 0,
    int Intelligence = 0)
{
    /// <summary>두 모디파이어를 항목별로 더한다(착용 세트 합산용).</summary>
    public EquipmentStatModifier Add(in EquipmentStatModifier other) => new(
        MaxHealth + other.MaxHealth,
        MaxMana + other.MaxMana,
        AttackPower + other.AttackPower,
        Defense + other.Defense,
        Strength + other.Strength,
        Dexterity + other.Dexterity,
        Intelligence + other.Intelligence);
}

/// <summary>
/// 장비 *정의*. 일반 아이템 정의(ItemDef)와 분리 — 장비만의 슬롯·스탯을 담는다(단일책임).
/// 둘 다 같은 itemId 로 묶인다(items.json 의 한 항목에서 파생).
/// </summary>
/// <param name="ItemId">카탈로그 키. ItemDef/InventoryItem 과 동일 식별자.</param>
/// <param name="Slot">착용 슬롯(공통 EquipmentType).</param>
/// <param name="Stats">착용 시 더해지는 가산 스탯.</param>
public sealed record EquipmentDef(
    string ItemId,
    EquipmentType Slot,
    EquipmentStatModifier Stats);

/// <summary>
/// 상점 진열 한 항목. 가격은 서버 권위 — 클라가 가격을 정하지 못한다.
/// 스탯 미리보기는 EquipmentDef 에서 파생(중복 저작 금지) — 여기엔 가격·분류만.
/// </summary>
/// <param name="ItemId">카탈로그 키.</param>
/// <param name="BuyPrice">구매가(골드). 양수.</param>
/// <param name="SellPrice">판매가(골드). 보통 BuyPrice 미만.</param>
/// <param name="Category">진열 분류(클라 탭). proto enum 과 정수값 1:1.</param>
public sealed record ShopItemDef(
    string ItemId,
    long BuyPrice,
    long SellPrice,
    ShopCategory Category);
