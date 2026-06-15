using Shared.Gameplay.Equipment;

namespace Game.System.Equipment
{
    /// <summary>
    /// 착용 한 칸의 도메인 DTO(proto 은닉). 슬롯(공통 EquipmentType) + itemId.
    /// 표시(아이콘·이름)는 Presentation 에서 ItemDisplayCatalog 로 합성.
    /// </summary>
    public readonly struct EquippedItemData
    {
        public readonly EquipmentType Slot;
        public readonly string ItemId;

        public EquippedItemData(EquipmentType slot, string itemId)
        {
            Slot = slot;
            ItemId = itemId;
        }
    }
}
