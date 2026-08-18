namespace Game.Presentation.Inventory
{
    /// <summary>인벤토리 View → Model 단일 진입 인텐트.</summary>
    public abstract class InventoryIntent
    {
        /// <summary>서버에서 인벤토리를 다시 읽는다(창 열 때).</summary>
        public sealed class Refresh : InventoryIntent
        {
            public static readonly Refresh Instance = new();
            private Refresh() { }
        }

        /// <summary>탭 선택. Category == null 이면 전체(All).</summary>
        public sealed class SelectTab : InventoryIntent
        {
            public readonly ItemCategory? Category;
            public SelectTab(ItemCategory? category) { Category = category; }
        }

        /// <summary>소모품 사용. 차감(서버 권위)→성공 시 회복·토스트는 Side Effect(OnConsumableUsed/OnToast).</summary>
        public sealed class UseItem : InventoryIntent
        {
            public readonly int ItemId;
            public UseItem(int itemId) { ItemId = itemId; }
        }

        /// <summary>장비 장착(서버 권위). 슬롯은 서버가 결정. 성공 시 토스트 + 장비창 자동 갱신(IEquipmentService.OnChanged).</summary>
        public sealed class EquipItem : InventoryIntent
        {
            public readonly int ItemId;
            public EquipItem(int itemId) { ItemId = itemId; }
        }

        /// <summary>판매(서버 권위, 1개). 인벤 차감→골드 적립. 가격 확인 팝업은 View 책임, 확인 시 이 인텐트 발행.</summary>
        public sealed class SellItem : InventoryIntent
        {
            public readonly int ItemId;
            public SellItem(int itemId) { ItemId = itemId; }
        }
    }
}
