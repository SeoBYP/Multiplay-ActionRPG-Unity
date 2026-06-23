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
        public Sprite GradeBackground { get; } // 등급 배경(Model이 GradeSpriteCatalog로 해석). null이면 배경 없음.

        public EquipmentSlotModel(EquipmentType slot, string itemId, string displayName, Sprite icon,
            Sprite gradeBackground = null)
        {
            Slot = slot;
            ItemId = itemId;
            DisplayName = displayName;
            Icon = icon;
            GradeBackground = gradeBackground;
        }
    }
}
