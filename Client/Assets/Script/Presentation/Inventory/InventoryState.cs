using System;
using System.Collections.Generic;

namespace Game.Presentation.Inventory
{
    /// <summary>
    /// 인벤토리 화면 상태(불변). SelectedCategory == null 은 "전체(All)" 탭.
    /// </summary>
    public sealed class InventoryState
    {
        public static readonly InventoryState Initial =
            new(Array.Empty<InventoryItemModel>(), selectedCategory: null, isLoading: false, error: null);

        public IReadOnlyList<InventoryItemModel> Items { get; }
        public ItemCategory? SelectedCategory { get; }
        public bool IsLoading { get; }
        public string Error { get; }

        public InventoryState(IReadOnlyList<InventoryItemModel> items, ItemCategory? selectedCategory, bool isLoading, string error)
        {
            Items = items;
            SelectedCategory = selectedCategory;
            IsLoading = isLoading;
            Error = error;
        }

        public InventoryState WithLoading() => new(Items, SelectedCategory, true, null);
        public InventoryState WithItems(IReadOnlyList<InventoryItemModel> items) => new(items, SelectedCategory, false, null);
        public InventoryState WithError(string error) => new(Items, SelectedCategory, false, error);
        public InventoryState WithSelectedCategory(ItemCategory? category) => new(Items, category, IsLoading, Error);
    }
}
