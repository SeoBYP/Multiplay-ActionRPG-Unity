using GameServer.Application.Domains.Codex;
using GameServer.Application.Domains.Inventory;
using GameServer.Tests.Infrastructure.Fakes.Repositories;

namespace GameServer.Tests.Application.Services;

public class InventoryServiceTests
{
    private readonly FakeInventoryRepository _repository = new();
    private readonly FakeCodexRepository _codexRepository = new();
    private readonly InventoryService _service;

    public InventoryServiceTests()
    {
        _service = new InventoryService(_repository, new CodexService(_codexRepository));
    }

    [Fact]
    public async Task 카탈로그에_있는_아이템_지급은_성공하고_수량이_누적된다()
    {
        var first = await _service.GrantItemAsync(1L, 1001, 2);
        var second = await _service.GrantItemAsync(1L, 1001, 3);

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Equal(5, second.NewQuantity);
    }

    [Fact]
    public async Task 카탈로그에_없는_아이템_지급은_실패한다()
    {
        var result = await _service.GrantItemAsync(1L, 1930, 1);

        Assert.False(result.Success);
        Assert.Equal(0, result.NewQuantity);
    }

    [Fact]
    public async Task 지급_수량이_0이하이면_실패한다()
    {
        Assert.False((await _service.GrantItemAsync(1L, 1001, 0)).Success);
        Assert.False((await _service.GrantItemAsync(1L, 1001, -5)).Success);
    }

    [Fact]
    public async Task MaxStack을_넘는_지급은_상한에서_멈춘다()
    {
        // potion_hp_small MaxStack = 99
        var result = await _service.GrantItemAsync(1L, 1001, 150);

        Assert.True(result.Success);
        Assert.Equal(99, result.NewQuantity);
    }

    [Fact]
    public async Task 보유한_아이템_소비는_성공하고_남은_수량을_반환한다()
    {
        await _service.GrantItemAsync(1L, 1001, 5);

        var result = await _service.ConsumeItemAsync(1L, 1001, 2);

        Assert.True(result.Success);
        Assert.Equal(3, result.RemainingQuantity);
    }

    [Fact]
    public async Task 전량_소비하면_남은_수량은_0이다()
    {
        await _service.GrantItemAsync(1L, 1001, 2);

        var result = await _service.ConsumeItemAsync(1L, 1001, 2);

        Assert.True(result.Success);
        Assert.Equal(0, result.RemainingQuantity);
        // 전량 소비 후엔 인벤토리에서 사라진다.
        Assert.Empty(await _service.GetInventoryAsync(1L));
    }

    [Fact]
    public async Task 보유보다_많이_소비하면_실패하고_변화가_없다()
    {
        await _service.GrantItemAsync(1L, 1001, 2);

        var result = await _service.ConsumeItemAsync(1L, 1001, 5);

        Assert.False(result.Success);
        var inv = await _service.GetInventoryAsync(1L);
        Assert.Equal(2, inv[0].Quantity);
    }

    [Fact]
    public async Task 미보유_아이템_소비는_실패한다()
    {
        var result = await _service.ConsumeItemAsync(1L, 1001, 1);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task 소비_수량이_0이하이면_실패한다()
    {
        await _service.GrantItemAsync(1L, 1001, 3);

        Assert.False((await _service.ConsumeItemAsync(1L, 1001, 0)).Success);
        Assert.False((await _service.ConsumeItemAsync(1L, 1001, -1)).Success);
    }

    [Fact]
    public async Task GetInventory는_유저의_보유_목록을_반환한다()
    {
        await _service.GrantItemAsync(1L, 1001, 2);
        await _service.GrantItemAsync(1L, 1002, 5);
        await _service.GrantItemAsync(2L, 1002, 1); // 다른 유저

        var inventory = await _service.GetInventoryAsync(1L);

        Assert.Equal(2, inventory.Count);
        Assert.Contains(inventory, i => i.ItemId == 1001 && i.Quantity == 2);
        Assert.Contains(inventory, i => i.ItemId == 1002 && i.Quantity == 5);
    }

    [Fact]
    public async Task 지급은_도감에_발견을_기록한다()
    {
        await _service.GrantItemAsync(1L, 1001, 1);

        var discovered = await _codexRepository.GetDiscoveredItemIdsAsync(1L);
        Assert.Contains(1001, discovered);
    }

    [Fact]
    public async Task 지급_실패시에는_도감에_기록되지_않는다()
    {
        await _service.GrantItemAsync(1L, 1930, 1); // 카탈로그 없음 → 실패

        Assert.Empty(await _codexRepository.GetDiscoveredItemIdsAsync(1L));
    }
}
