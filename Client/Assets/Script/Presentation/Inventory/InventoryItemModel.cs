using UnityEngine;

namespace Game.Presentation.Inventory
{
    /// <summary>
    /// 인벤토리 슬롯 1개의 View-facing 모델. GUI는 이 타입만 본다(proto·System DTO 비노출).
    /// 표시 데이터(이름·아이콘·분류)는 ItemDisplayCatalog에서 합성된다.
    /// </summary>
    public sealed class InventoryItemModel
    {
        public int ItemId { get; }
        public int Quantity { get; }
        public string DisplayName { get; }
        public Sprite Icon { get; }
        public ItemCategory Category { get; }
        public ItemGrade Grade { get; }
        public Sprite GradeBackground { get; } // 등급 배경 스프라이트(Model이 GradeSpriteCatalog로 해석). null이면 배경 없음.

        public InventoryItemModel(int itemId, int quantity, string displayName, Sprite icon,
            ItemCategory category, ItemGrade grade = ItemGrade.Common, Sprite gradeBackground = null)
        {
            ItemId = itemId;
            Quantity = quantity;
            DisplayName = displayName;
            Icon = icon;
            Category = category;
            Grade = grade;
            GradeBackground = gradeBackground;
        }
    }
}
