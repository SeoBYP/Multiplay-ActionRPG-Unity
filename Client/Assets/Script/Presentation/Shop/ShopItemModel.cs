using System.Collections.Generic;
using UnityEngine;

namespace Game.Presentation.Shop
{
    /// <summary>상점 진열 한 항목(View 표시용). 서버 가격/분류/스탯 + 클라 카탈로그 이름·아이콘 합성.</summary>
    public sealed class ShopItemModel
    {
        public string ItemId { get; }
        public string DisplayName { get; }
        public Sprite Icon { get; }
        public long BuyPrice { get; }
        public long SellPrice { get; }
        public ShopCategory Category { get; }
        public IReadOnlyList<ShopStatLine> Stats { get; }

        public ShopItemModel(string itemId, string displayName, Sprite icon, long buyPrice, long sellPrice,
            ShopCategory category, IReadOnlyList<ShopStatLine> stats)
        {
            ItemId = itemId;
            DisplayName = displayName;
            Icon = icon;
            BuyPrice = buyPrice;
            SellPrice = sellPrice;
            Category = category;
            Stats = stats;
        }
    }

    /// <summary>진열 스탯 한 줄(예: 공격력 +5). 선택 패널의 ShopItemStatusSlot 채움.</summary>
    public readonly struct ShopStatLine
    {
        public readonly string Stat;
        public readonly int Amount;

        public ShopStatLine(string stat, int amount)
        {
            Stat = stat;
            Amount = amount;
        }
    }
}
