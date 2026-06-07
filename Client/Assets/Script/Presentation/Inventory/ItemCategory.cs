namespace Game.Presentation.Inventory
{
    /// <summary>
    /// 아이템 분류(탭 필터 기준). 클라 카탈로그가 소유 — 서버 proto는 itemId+수량만 보낸다.
    /// 탭의 "All"은 분류가 아니므로 여기 없다(InventoryState.SelectedCategory == null = 전체).
    /// </summary>
    public enum ItemCategory
    {
        Equipment,   // 장비
        Consumable,  // 소비 (포션 등)
        Material,    // 재료 (강화/제작)
        Quest,       // 퀘스트 아이템
        Etc,         // 기타
    }
}
