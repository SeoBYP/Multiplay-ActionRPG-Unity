using System.Collections.Generic;

namespace Game.System.Shop
{
    /// <summary>상점 진열 한 항목(proto 비노출 도메인 DTO). 이름·아이콘은 클라가 itemId 로 자기 카탈로그 룩업.</summary>
    public readonly struct ShopItemData
    {
        public readonly int ItemId;
        public readonly long BuyPrice;
        public readonly long SellPrice;
        public readonly ShopCategory Category;
        public readonly IReadOnlyList<ShopStatData> Stats;

        public ShopItemData(int itemId, long buyPrice, long sellPrice, ShopCategory category, IReadOnlyList<ShopStatData> stats)
        {
            ItemId = itemId;
            BuyPrice = buyPrice;
            SellPrice = sellPrice;
            Category = category;
            Stats = stats;
        }
    }

    /// <summary>진열 스탯 미리보기 한 줄(예: AttackPower +5).</summary>
    public readonly struct ShopStatData
    {
        public readonly string Stat;
        public readonly int Amount;

        public ShopStatData(string stat, int amount)
        {
            Stat = stat;
            Amount = amount;
        }
    }
}
