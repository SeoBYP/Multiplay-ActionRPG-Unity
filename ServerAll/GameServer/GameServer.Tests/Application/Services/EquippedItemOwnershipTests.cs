using GameServer.Application.Domains.Codex;
using GameServer.Application.Domains.Equipment;
using GameServer.Application.Domains.Inventory;
using GameServer.Application.Domains.Shop;
using GameServer.Application.Domains.Wallet;
using GameServer.Tests.Infrastructure.Fakes.Repositories;

namespace GameServer.Tests.Application.Services;

/// <summary>
/// G4 — 착용 장비의 스탯은 <b>지금 보유한</b> 장비에서만 나와야 한다.
///
/// 지금은 착용 시점에만 소유를 확인하고, 판매(<c>SellAsync</c>)·소비(<c>ConsumeItem</c>)는 착용 여부를 보지 않는다.
/// 그래서 "착용 → 판매" 하면 골드를 받고도 공격력이 그대로 남는다.
///
/// 고치는 방법은 둘(착용 중 판매 거부 / 판매 시 자동 해제)인데, 어느 쪽이든 이 불변식은 지켜야 한다
/// → 테스트는 방법이 아니라 불변식을 고정한다.
/// </summary>
public class EquippedItemOwnershipTests
{
    private const long UserId = 1L;
    private const int Sword = 2101; // 무기, maxStack 1, 상점 판매가 50

    private readonly FakeInventoryRepository _invRepo = new();
    private readonly EquipmentService _equipment;
    private readonly ShopService _shop;
    private readonly InventoryService _inventory;

    public EquippedItemOwnershipTests()
    {
        _inventory = new InventoryService(_invRepo, new CodexService(new FakeCodexRepository()));
        _equipment = new EquipmentService(new FakeEquipmentRepository(), _inventory);
        _shop = new ShopService(new WalletService(new FakeWalletRepository()), _inventory);
    }

    [Fact]
    public async Task 대조군_착용한_장비는_스탯을_준다()
    {
        await _invRepo.AddQuantityAsync(UserId, Sword, 1, maxStack: 1);
        await _equipment.EquipAsync(UserId, Sword);

        Assert.NotEqual(default, await _equipment.GetEquippedStatsAsync(UserId));
    }

    [Fact]
    public async Task 착용한_장비를_팔면_그_장비의_스탯은_남지_않는다()
    {
        await _invRepo.AddQuantityAsync(UserId, Sword, 1, maxStack: 1);
        await _equipment.EquipAsync(UserId, Sword);

        var sell = await _shop.SellAsync(UserId, Sword, 1);

        var stats = await _equipment.GetEquippedStatsAsync(UserId);
        Assert.False(sell.Success && stats != default,
            "판매 골드를 받았는데 그 장비의 스탯도 여전히 붙어 있다(무한 복제 경로).");
    }

    [Fact]
    public async Task 착용한_장비를_소비해도_그_장비의_스탯은_남지_않는다()
    {
        await _invRepo.AddQuantityAsync(UserId, Sword, 1, maxStack: 1);
        await _equipment.EquipAsync(UserId, Sword);

        var consume = await _inventory.ConsumeItemAsync(UserId, Sword, 1);

        var stats = await _equipment.GetEquippedStatsAsync(UserId);
        Assert.False(consume.Success && stats != default,
            "장비가 인벤토리에서 사라졌는데 스탯은 남아 있다.");
    }
}
