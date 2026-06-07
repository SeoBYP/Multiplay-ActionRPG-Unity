using GameServer.Domain.Entities.Inventory;

namespace GameServer.Tests.Domain.Entities;

public class InventoryItemTests
{
    [Fact]
    public void Create_하면_소유_수량으로_시작한다()
    {
        var item = InventoryItem.Create(userId: 1L, itemId: "potion_hp_small", quantity: 3);

        Assert.Equal(1L, item.UserId);
        Assert.Equal("potion_hp_small", item.ItemId);
        Assert.Equal(3, item.Quantity);
        Assert.True(item.UpdatedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void Create_는_잘못된_인자에_예외를_던진다()
    {
        Assert.Throws<ArgumentException>(() => InventoryItem.Create(0L, "potion_hp_small", 1));
        Assert.Throws<ArgumentException>(() => InventoryItem.Create(1L, "", 1));
        Assert.Throws<ArgumentException>(() => InventoryItem.Create(1L, "potion_hp_small", 0));
        Assert.Throws<ArgumentException>(() => InventoryItem.Create(1L, "potion_hp_small", -1));
    }

    [Fact]
    public void 수량을_더하면_누적된다()
    {
        var item = InventoryItem.Create(1L, "potion_hp_small", 1);

        item.Add(2);
        item.Add(3);

        Assert.Equal(6, item.Quantity);
    }

    [Fact]
    public void 더하는_수량이_0이하이면_무시된다()
    {
        var item = InventoryItem.Create(1L, "potion_hp_small", 5);

        item.Add(0);
        item.Add(-3);

        Assert.Equal(5, item.Quantity);
    }

    [Fact]
    public void 보유한_만큼_차감하면_성공하고_수량이_준다()
    {
        var item = InventoryItem.Create(1L, "potion_hp_small", 5);

        var removed = item.Remove(2);

        Assert.True(removed);
        Assert.Equal(3, item.Quantity);
    }

    [Fact]
    public void 보유보다_많이_차감하면_실패하고_수량이_그대로다()
    {
        var item = InventoryItem.Create(1L, "potion_hp_small", 2);

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
        Assert.True(ItemCatalog.Contains("potion_hp_small"));

        var def = ItemCatalog.Get("potion_hp_small");

        Assert.NotNull(def);
        Assert.Equal("potion_hp_small", def!.ItemId);
        Assert.True(def.Stackable);
        Assert.True(def.MaxStack > 0);
    }

    [Fact]
    public void 정의되지_않은_아이템은_null이고_Contains는_false다()
    {
        Assert.False(ItemCatalog.Contains("unknown_item"));
        Assert.Null(ItemCatalog.Get("unknown_item"));
    }
}
