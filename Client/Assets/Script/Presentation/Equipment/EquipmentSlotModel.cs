using Shared.Gameplay.Equipment;
using UnityEngine;

namespace Game.Presentation.Equipment
{
    /// <summary>
    /// 착용 슬롯 1칸의 View-facing 모델. GUI는 이 타입만 본다(proto·System DTO 비노출).
    /// 표시(이름·아이콘)는 ItemDisplayCatalog 에서 합성. 빈 슬롯은 State 에 포함하지 않는다(착용분만).
    /// </summary>
    public sealed class EquipmentSlotModel
    {
        public EquipmentType Slot { get; }
        public string ItemId { get; }
        public string DisplayName { get; }
        public Sprite Icon { get; }

        public EquipmentSlotModel(EquipmentType slot, string itemId, string displayName, Sprite icon)
        {
            Slot = slot;
            ItemId = itemId;
            DisplayName = displayName;
            Icon = icon;
        }
    }
}
