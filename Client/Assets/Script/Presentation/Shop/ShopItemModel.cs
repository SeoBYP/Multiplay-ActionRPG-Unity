using System.Collections.Generic;
using UnityEngine;

namespace Game.Presentation.Shop
{
    /// <summary>상점 진열 한 항목(View 표시용). 서버 가격/분류/스탯 + 클라 카탈로그 이름·아이콘 합성.</summary>
    public sealed class ShopItemModel
    {
        public string ItemId { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public Sprite Icon { get; }
        public long BuyPrice { get; }
        public long SellPrice { get; }
        public ShopCategory Category { get; }
        public IReadOnlyList<ShopStatLine> Stats { get; }
        public Sprite GradeBackground { get; } // 등급 배경(Model이 GradeSpriteCatalog로 해석). null이면 배경 없음.

        public ShopItemModel(string itemId, string displayName, Sprite icon, long buyPrice, long sellPrice,
            ShopCategory category, IReadOnlyList<ShopStatLine> stats, Sprite gradeBackground = null, string description = null)
        {
            ItemId = itemId;
            DisplayName = displayName;
            Description = description ?? string.Empty;
            Icon = icon;
            BuyPrice = buyPrice;
            SellPrice = sellPrice;
            Category = category;
            Stats = stats;
            GradeBackground = gradeBackground;
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
