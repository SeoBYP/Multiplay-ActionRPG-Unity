using GameServer.Domain.Entities.Inventory;
using Shared.Infrastructure.Items;

namespace GameServer.Tests.Domain.Entities;

public class InventoryItemTests
{
    [Fact]
    public void Create_하면_소유_수량으로_시작한다()
    {
        var item = InventoryItem.Create(userId: 1L, itemId: 1001, quantity: 3);

        Assert.Equal(1L, item.UserId);
        Assert.Equal(1001, item.ItemId);
        Assert.Equal(3, item.Quantity);
        Assert.True(item.UpdatedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void Create_는_잘못된_인자에_예외를_던진다()
    {
        Assert.Throws<ArgumentException>(() => InventoryItem.Create(0L, 1001, 1));
        Assert.Throws<ArgumentException>(() => InventoryItem.Create(1L, 0, 1));   // 0 = 미배정 itemId
        Assert.Throws<ArgumentException>(() => InventoryItem.Create(1L, 1001, 0));
        Assert.Throws<ArgumentException>(() => InventoryItem.Create(1L, 1001, -1));
    }

    [Fact]
    public void 수량을_더하면_누적된다()
    {
        var item = InventoryItem.Create(1L, 1001, 1);

        item.Add(2);
        item.Add(3);

        Assert.Equal(6, item.Quantity);
    }

    [Fact]
    public void 더하는_수량이_0이하이면_무시된다()
    {
        var item = InventoryItem.Create(1L, 1001, 5);

        item.Add(0);
        item.Add(-3);

        Assert.Equal(5, item.Quantity);
    }

    [Fact]
    public void 보유한_만큼_차감하면_성공하고_수량이_준다()
    {
        var item = InventoryItem.Create(1L, 1001, 5);

        var removed = item.Remove(2);

        Assert.True(removed);
        Assert.Equal(3, item.Quantity);
    }

    [Fact]
    public void 보유보다_많이_차감하면_실패하고_수량이_그대로다()
    {
        var item = InventoryItem.Create(1L, 1001, 2);

        var removed = item.Remove(3);

        Assert.False(removed);
        Assert.Equal(2, item.Quantity);
    }
}

public class ItemCatalogTests
{
    [Fact]
    public void 카탈로그에_정의된_아이템은_조회된다()
    {
        Assert.True(ItemCatalog.Contains(1001));

        var def = ItemCatalog.Get(1001);

        Assert.NotNull(def);
        Assert.Equal(1001, def!.NumericId);   // ItemDef.ItemId 는 저작 문자열 키(로그·디버깅용)로 남는다
        Assert.True(def.Stackable);
        Assert.True(def.MaxStack > 0);
    }

    [Fact]
    public void 정의되지_않은_아이템은_null이고_Contains는_false다()
    {
        Assert.False(ItemCatalog.Contains(1930));
        Assert.Null(ItemCatalog.Get(1930));
    }
}
