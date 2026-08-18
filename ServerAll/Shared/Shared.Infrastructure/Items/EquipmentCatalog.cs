namespace Shared.Infrastructure.Items;

/// <summary>
/// 장비 정의 카탈로그 — items.json(bake) 중 `isEquipment` 항목의 파사드.
/// 착용 상태(UserId,Slot→ItemId)만 DB(user_equipments)에 영속한다.
/// 공개 API 는 구 `GameServer.Domain.Entities.Equipment.EquipmentCatalog` 와 동일하다(호출부 무변경).
/// </summary>
public static class EquipmentCatalog
{
    /// <summary>장비 정의가 존재하는 itemId 인지(= 장착 가능한가).</summary>
    public static bool IsEquippable(string itemId) => ItemCatalogData.Current.EquipmentById.ContainsKey(itemId);

    /// <summary>장비 정의를 반환. 장비가 아니면 null.</summary>
    public static EquipmentDef? Get(string itemId) => ItemCatalogData.Current.EquipmentById.GetValueOrDefault(itemId);

    /// <summary>전체 정의(저작 순서).</summary>
    public static IReadOnlyCollection<EquipmentDef> All => ItemCatalogData.Current.Equipment;
}
