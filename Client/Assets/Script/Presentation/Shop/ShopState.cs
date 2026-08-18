using System;
using System.Collections.Generic;

namespace Game.Presentation.Shop
{
    /// <summary>상점 화면 상태(불변). SelectedCategory==null = 전체 탭. SelectedItemId==null = 선택 없음(빈 패널).</summary>
    public sealed class ShopState
    {
        public static readonly ShopState Initial = new(
            Array.Empty<ShopItemModel>(), selectedCategory: null, selectedItemId: null,
            quantity: 1, gold: 0, isLoading: false, error: null);

        public IReadOnlyList<ShopItemModel> Items { get; }
        public ShopCategory? SelectedCategory { get; }
        public int? SelectedItemId { get; }
        public int Quantity { get; }
        public long Gold { get; }
        public bool IsLoading { get; }
        public string Error { get; }

        public ShopState(IReadOnlyList<ShopItemModel> items, ShopCategory? selectedCategory, int? selectedItemId,
            int quantity, long gold, bool isLoading, string error)
        {
            Items = items;
            SelectedCategory = selectedCategory;
            SelectedItemId = selectedItemId;
            Quantity = quantity;
            Gold = gold;
            IsLoading = isLoading;
            Error = error;
        }

        public ShopState With(
            IReadOnlyList<ShopItemModel> items = null,
            ShopCategory? selectedCategory = null, bool clearCategory = false,
            int? selectedItemId = null, bool clearItem = false,
            int? quantity = null, long? gold = null, bool? isLoading = null, string error = null, bool clearError = false)
            => new(
                items ?? Items,
                clearCategory ? null : (selectedCategory ?? SelectedCategory),
                clearItem ? null : (selectedItemId ?? SelectedItemId),
                quantity ?? Quantity,
                gold ?? Gold,
                isLoading ?? IsLoading,
                clearError ? null : (error ?? Error));

        /// <summary>현재 선택된 항목(없으면 null).</summary>
        public ShopItemModel Selected
        {
            get
            {
                if (SelectedItemId == null) return null;
                foreach (var item in Items)
                    if (item.ItemId == SelectedItemId) return item;
                return null;
            }
        }
    }
}
