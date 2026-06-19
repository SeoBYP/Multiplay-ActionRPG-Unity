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
            new(Array.Empty<InventoryItemModel>(), selectedCategory: null, isLoading: false, error: null, gold: 0);

        public IReadOnlyList<InventoryItemModel> Items { get; }
        public ItemCategory? SelectedCategory { get; }
        public bool IsLoading { get; }
        public string Error { get; }

        /// <summary>현재 캐릭터의 골드 잔액(서버 권위, 지갑). 인벤토리 새로고침 시 함께 로드된다.</summary>
        public long Gold { get; }

        public InventoryState(IReadOnlyList<InventoryItemModel> items, ItemCategory? selectedCategory, bool isLoading, string error, long gold)
        {
            Items = items;
            SelectedCategory = selectedCategory;
            IsLoading = isLoading;
            Error = error;
            Gold = gold;
        }

        public InventoryState WithLoading() => new(Items, SelectedCategory, true, null, Gold);
        public InventoryState WithItems(IReadOnlyList<InventoryItemModel> items) => new(items, SelectedCategory, false, null, Gold);
        public InventoryState WithError(string error) => new(Items, SelectedCategory, false, error, Gold);
        public InventoryState WithSelectedCategory(ItemCategory? category) => new(Items, category, IsLoading, Error, Gold);
        public InventoryState WithGold(long gold) => new(Items, SelectedCategory, IsLoading, Error, gold);
    }
}
