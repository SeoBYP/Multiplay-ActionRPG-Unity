using Shared.Gameplay.Equipment;

namespace GameServer.Domain.Entities.Equipment;

/// <summary>
/// 장비 *정의*(정적 기획데이터). 일반 아이템 정의(ItemDef)와 분리 — 장비만의 슬롯·스탯을 담는다.
/// ItemDef 는 표시/스택 메타를, EquipmentDef 는 착용 슬롯과 스탯 모디파이어를 소유(단일책임).
/// 둘 다 같은 itemId 로 묶인다(ItemCatalog 에 소유 메타, EquipmentCatalog 에 장비 메타).
/// </summary>
/// <param name="ItemId">카탈로그 키. ItemCatalog/InventoryItem 과 동일 식별자.</param>
/// <param name="Slot">착용 슬롯(공통 EquipmentType).</param>
/// <param name="Stats">착용 시 더해지는 가산 스탯.</param>
public sealed record EquipmentDef(
    string ItemId,
    EquipmentType Slot,
    EquipmentStatModifier Stats);
