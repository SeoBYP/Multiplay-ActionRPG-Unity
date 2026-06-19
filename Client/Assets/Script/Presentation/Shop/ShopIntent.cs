namespace Game.Presentation.Shop
{
    /// <summary>상점 View → Model 의도. View는 Accept(Intent)만 호출한다.</summary>
    public abstract class ShopIntent
    {
        /// <summary>진열·골드 새로고침(열 때).</summary>
        public sealed class Refresh : ShopIntent
        {
            public static readonly Refresh Instance = new();
            private Refresh() { }
        }

        /// <summary>탭 선택(null=전체).</summary>
        public sealed class SelectTab : ShopIntent
        {
            public readonly ShopCategory? Category;
            public SelectTab(ShopCategory? category) => Category = category;
        }

        /// <summary>리스트 항목 선택 → 선택 패널 표시. 수량은 1로 초기화.</summary>
        public sealed class SelectItem : ShopIntent
        {
            public readonly string ItemId;
            public SelectItem(string itemId) => ItemId = itemId;
        }

        /// <summary>구매 수량 설정(1 미만은 1로 클램프). +/- 버튼·입력칸이 사용.</summary>
        public sealed class SetQuantity : ShopIntent
        {
            public readonly int Quantity;
            public SetQuantity(int quantity) => Quantity = quantity;
        }

        /// <summary>선택 항목을 현재 수량만큼 구매.</summary>
        public sealed class Buy : ShopIntent
        {
            public static readonly Buy Instance = new();
            private Buy() { }
        }
    }
}
