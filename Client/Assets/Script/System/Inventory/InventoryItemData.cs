namespace Game.System.Inventory
{
    /// <summary>
    /// 인벤토리 아이템 1개의 도메인 표현(proto에서 변환).
    /// 상위 레이어(Presentation/GUI)가 proto(InventoryItemInfo)에 의존하지 않도록 한다.
    /// 정의(이름·아이콘·분류)는 클라 카탈로그가 itemId로 룩업 — 여기엔 itemId+수량만.
    /// </summary>
    public readonly struct InventoryItemData
    {
        public readonly int ItemId;
        public readonly int Quantity;

        public InventoryItemData(int itemId, int quantity)
        {
            ItemId = itemId;
            Quantity = quantity;
        }
    }
}
