using UnityEngine;

namespace Game.Presentation.Inventory
{
    /// <summary>
    /// 인벤토리 슬롯 1개의 View-facing 모델. GUI는 이 타입만 본다(proto·System DTO 비노출).
    /// 표시 데이터(이름·아이콘·분류)는 ItemDisplayCatalog에서 합성된다.
    /// </summary>
    public sealed class InventoryItemModel
    {
        public string ItemId { get; }
        public int Quantity { get; }
        public string DisplayName { get; }
        public Sprite Icon { get; }
        public ItemCategory Category { get; }

        public InventoryItemModel(string itemId, int quantity, string displayName, Sprite icon, ItemCategory category)
        {
            ItemId = itemId;
            Quantity = quantity;
            DisplayName = displayName;
            Icon = icon;
            Category = category;
        }
    }
}
