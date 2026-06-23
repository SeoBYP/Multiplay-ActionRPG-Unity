using GameServer.Application.Domains.Inventory;
using GameServer.Application.Domains.Inventory.Interfaces;
using GameServer.Application.Domains.Shop;
using GameServer.Application.Domains.Wallet;
using GameServer.Domain.Entities.Inventory;
using GameServer.Tests.Infrastructure.Fakes.Repositories;
using AppInventoryService = GameServer.Application.Domains.Inventory.InventoryService;
using AppWalletService = GameServer.Application.Domains.Wallet.WalletService;

namespace GameServer.Tests.Application.Services;

/// <summary>
/// 상점 구매/판매 — 지갑·인벤토리 조합(원자성). 실제 Wallet/Inventory 서비스 + 인메모리 저장소로 검증.
/// potion_hp_small: Buy 50 / Sell 10.
/// </summary>
public class ShopServiceTests
{
    private const long UserId = 1L;
    private const string Potion = "potion_hp_small";

    private readonly FakeWalletRepository _walletRepo = new();
    private readonly FakeInventoryRepository _invRepo = new();
    private readonly AppWalletService _wallet;
    private readonly AppInventoryService _inventory;
    private readonly ShopService _shop;

    public ShopServiceTests()
    {
        _wallet = new AppWalletService(_walletRepo);
        _inventory = new AppInventoryService(_invRepo, new GameServer.Application.Domains.Codex.CodexService(new FakeCodexRepository()));
        _shop = new ShopService(_wallet, _inventory);
    }

    [Fact]
    public async Task 구매_성공하면_골드차감되고_아이템지급된다()
    {
        await _wallet.AddAsync(UserId, 100);

        var result = await _shop.BuyAsync(UserId, Potion, 1);

        Assert.True(result.Success, result.FailReason);
        Assert.Equal(50, result.Gold);          // 100 - 50
        Assert.Equal(1, result.NewQuantity);
        Assert.Equal(50, await _wallet.GetBalanceAsync(UserId));
    }

    [Fact]
    public async Task 잔액부족이면_구매거부되고_변화없다()
    {
        await _wallet.AddAsync(UserId, 30); // 50 미만

        var result = await _shop.BuyAsync(UserId, Potion, 1);

        Assert.False(result.Success);
        Assert.Equal(30, await _wallet.GetBalanceAsync(UserId));        // 골드 그대로
        Assert.Empty(await _inventory.GetInventoryAsync(UserId));        // 아이템 미지급
    }

    [Fact]
    public async Task 안파는_아이템은_구매거부된다()
    {
        await _wallet.AddAsync(UserId, 1000);

        var result = await _shop.BuyAsync(UserId, "no_such_item", 1);

        Assert.False(result.Success);
        Assert.Equal(1000, await _wallet.GetBalanceAsync(UserId)); // 차감 안 함
    }

    [Fact]
    public async Task 지급실패하면_차감골드가_환불된다()
    {
        // 지급이 실패하도록 강제(설정 오류 시뮬) → 차감분 환불 검증(보상 트랜잭션).
        var shop = new ShopService(_wallet, new FailingInventoryService());
        await _wallet.AddAsync(UserId, 100);

        var result = await shop.BuyAsync(UserId, Potion, 1);

        Assert.False(result.Success);
        Assert.Equal(100, await _wallet.GetBalanceAsync(UserId)); // 환불되어 원복
    }

    [Fact]
    public async Task 판매_성공하면_아이템차감되고_골드적립된다()
    {
        await _inventory.GrantItemAsync(UserId, Potion, 3);

        var result = await _shop.SellAsync(UserId, Potion, 2);

        Assert.True(result.Success, result.FailReason);
        Assert.Equal(20, result.Gold);            // 10 * 2
        Assert.Equal(1, result.RemainingQuantity); // 3 - 2
        Assert.Equal(20, await _wallet.GetBalanceAsync(UserId));
    }

    [Fact]
    public async Task 미보유면_판매거부되고_골드미적립()
    {
        var result = await _shop.SellAsync(UserId, Potion, 1);

        Assert.False(result.Success);
        Assert.Equal(0, await _wallet.GetBalanceAsync(UserId));
    }

    [Fact]
    public async Task 안파는_아이템은_판매거부된다()
    {
        var result = await _shop.SellAsync(UserId, "no_such_item", 1);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task 수량이_0이하이면_구매판매_모두_거부된다()
    {
        Assert.False((await _shop.BuyAsync(UserId, Potion, 0)).Success);
        Assert.False((await _shop.SellAsync(UserId, Potion, -1)).Success);
    }

    /// <summary>지급을 항상 실패시키는 인벤토리(환불 경로 검증용).</summary>
    private sealed class FailingInventoryService : IInventoryService
    {
        public Task<List<InventoryItem>> GetInventoryAsync(long userId, CancellationToken ct = default)
            => Task.FromResult(new List<InventoryItem>());

        public Task<ItemGrantResult> GrantItemAsync(long userId, string itemId, int amount, CancellationToken ct = default)
            => Task.FromResult(ItemGrantResult.Fail(itemId, "forced"));

        public Task<ItemConsumeResult> ConsumeItemAsync(long userId, string itemId, int amount, CancellationToken ct = default)
            => Task.FromResult(ItemConsumeResult.Fail(itemId, "forced"));
    }
}
