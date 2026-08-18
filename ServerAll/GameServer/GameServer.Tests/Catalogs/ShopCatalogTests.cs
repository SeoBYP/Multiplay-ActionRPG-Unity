using Shared.Infrastructure.Items;

namespace GameServer.Tests.Domain.Entities;

public class ShopCatalogTests
{
    [Fact]
    public void 모든_상점아이템은_ItemCatalog에_존재한다()
    {
        // 상점이 파는 itemId 는 지급 경로(GrantItemAsync→ItemCatalog 검증)를 통과해야 한다 — 누락 시 구매가 환불로 끝남.
        foreach (var item in ShopCatalog.All)
            Assert.True(ItemCatalog.Contains(item.ItemId), $"ShopCatalog '{item.ItemId}' 가 ItemCatalog 에 없음");
    }

    [Fact]
    public void 가격은_양수이고_판매가는_구매가_이하다()
    {
        foreach (var item in ShopCatalog.All)
        {
            Assert.True(item.BuyPrice > 0, $"{item.ItemId} BuyPrice 양수 아님");
            Assert.True(item.SellPrice > 0, $"{item.ItemId} SellPrice 양수 아님");
            Assert.True(item.SellPrice <= item.BuyPrice, $"{item.ItemId} SellPrice > BuyPrice");
        }
    }

    [Fact]
    public void Get은_안파는_itemId에_null을_반환한다()
    {
        Assert.NotNull(ShopCatalog.Get("potion_hp_small"));
        Assert.Null(ShopCatalog.Get("no_such_item"));
    }
}
