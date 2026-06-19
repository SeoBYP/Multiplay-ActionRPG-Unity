using GameServer.Application.Domains.Inventory;
using GameServer.Tests.Infrastructure.Fakes.Repositories;

namespace GameServer.Tests.Application.Services;

public class InventoryServiceTests
{
    private readonly FakeInventoryRepository _repository = new();
    private readonly InventoryService _service;

    public InventoryServiceTests()
    {
        _service = new InventoryService(_repository);
    }

    [Fact]
    public async Task 카탈로그에_있는_아이템_지급은_성공하고_수량이_누적된다()
    {
        var first = await _service.GrantItemAsync(1L, "potion_hp_small", 2);
        var second = await _service.GrantItemAsync(1L, "potion_hp_small", 3);

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Equal(5, second.NewQuantity);
    }

    [Fact]
    public async Task 카탈로그에_없는_아이템_지급은_실패한다()
    {
        var result = await _service.GrantItemAsync(1L, "unknown_item", 1);

        Assert.False(result.Success);
        Assert.Equal(0, result.NewQuantity);
    }

    [Fact]
    public async Task 지급_수량이_0이하이면_실패한다()
    {
        Assert.False((await _service.GrantItemAsync(1L, "potion_hp_small", 0)).Success);
        Assert.False((await _service.GrantItemAsync(1L, "potion_hp_small", -5)).Success);
    }

    [Fact]
    public async Task MaxStack을_넘는_지급은_상한에서_멈춘다()
    {
        // potion_hp_small MaxStack = 99
        var result = await _service.GrantItemAsync(1L, "potion_hp_small", 150);

        Assert.True(result.Success);
        Assert.Equal(99, result.NewQuantity);
    }

    [Fact]
    public async Task 보유한_아이템_소비는_성공하고_남은_수량을_반환한다()
    {
        await _service.GrantItemAsync(1L, "potion_hp_small", 5);

        var result = await _service.ConsumeItemAsync(1L, "potion_hp_small", 2);

        Assert.True(result.Success);
        Assert.Equal(3, result.RemainingQuantity);
    }

    [Fact]
    public async Task 전량_소비하면_남은_수량은_0이다()
    {
        await _service.GrantItemAsync(1L, "potion_hp_small", 2);

        var result = await _service.ConsumeItemAsync(1L, "potion_hp_small", 2);

        Assert.True(result.Success);
        Assert.Equal(0, result.RemainingQuantity);
        // 전량 소비 후엔 인벤토리에서 사라진다.
        Assert.Empty(await _service.GetInventoryAsync(1L));
    }

    [Fact]
    public async Task 보유보다_많이_소비하면_실패하고_변화가_없다()
    {
        await _service.GrantItemAsync(1L, "potion_hp_small", 2);

        var result = await _service.ConsumeItemAsync(1L, "potion_hp_small", 5);

        Assert.False(result.Success);
        var inv = await _service.GetInventoryAsync(1L);
        Assert.Equal(2, inv[0].Quantity);
    }

    [Fact]
    public async Task 미보유_아이템_소비는_실패한다()
    {
        var result = await _service.ConsumeItemAsync(1L, "potion_hp_small", 1);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task 소비_수량이_0이하이면_실패한다()
    {
        await _service.GrantItemAsync(1L, "potion_hp_small", 3);

        Assert.False((await _service.ConsumeItemAsync(1L, "potion_hp_small", 0)).Success);
        Assert.False((await _service.ConsumeItemAsync(1L, "potion_hp_small", -1)).Success);
    }

    [Fact]
    public async Task GetInventory는_유저의_보유_목록을_반환한다()
    {
        await _service.GrantItemAsync(1L, "potion_hp_small", 2);
        await _service.GrantItemAsync(1L, "potion_mp_small", 5);
        await _service.GrantItemAsync(2L, "potion_mp_small", 1); // 다른 유저

        var inventory = await _service.GetInventoryAsync(1L);

        Assert.Equal(2, inventory.Count);
        Assert.Contains(inventory, i => i.ItemId == "potion_hp_small" && i.Quantity == 2);
        Assert.Contains(inventory, i => i.ItemId == "potion_mp_small" && i.Quantity == 5);
    }
}
