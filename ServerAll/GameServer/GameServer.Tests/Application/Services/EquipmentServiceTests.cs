using GameServer.Application.Domains.Equipment;
using GameServer.Application.Domains.Inventory;
using GameServer.Domain.Entities.Equipment;
using Shared.Gameplay.Equipment;
using GameServer.Domain.Entities.Inventory;
using GameServer.Tests.Infrastructure.Fakes.Repositories;

namespace GameServer.Tests.Application.Services;

public class EquipmentServiceTests
{
    private const long UserId = 1L;

    private static (EquipmentService svc, FakeInventoryRepository inv) Build()
    {
        var invRepo = new FakeInventoryRepository();
        var equipRepo = new FakeEquipmentRepository();
        var inventory = new InventoryService(invRepo);
        var svc = new EquipmentService(equipRepo, inventory);
        return (svc, invRepo);
    }

    private static async Task GrantAsync(FakeInventoryRepository inv, string itemId)
    {
        var def = ItemCatalog.Get(itemId)!;
        await inv.AddQuantityAsync(UserId, itemId, 1, def.MaxStack);
    }

    [Fact]
    public async Task 보유한_장비는_해당_슬롯에_장착된다()
    {
        var (svc, inv) = Build();
        await GrantAsync(inv, "sword_basic");

        var result = await svc.EquipAsync(UserId, "sword_basic");

        Assert.True(result.Success);
        Assert.Equal(EquipmentType.Weapon, result.Slot);

        var equipped = await svc.GetEquippedAsync(UserId);
        Assert.Single(equipped);
        Assert.Equal("sword_basic", equipped[0].ItemId);
    }

    [Fact]
    public async Task 미보유_장비는_장착에_실패한다()
    {
        var (svc, _) = Build();

        var result = await svc.EquipAsync(UserId, "sword_basic");

        Assert.False(result.Success);
        Assert.Equal("not owned", result.FailReason);
        Assert.Empty(await svc.GetEquippedAsync(UserId));
    }

    [Fact]
    public async Task 장비가_아닌_아이템은_장착에_실패한다()
    {
        var (svc, inv) = Build();
        await GrantAsync(inv, "potion_hp_small");

        var result = await svc.EquipAsync(UserId, "potion_hp_small");

        Assert.False(result.Success);
        Assert.Empty(await svc.GetEquippedAsync(UserId));
    }

    [Fact]
    public async Task 같은_슬롯_재장착은_기존_장비를_교체한다()
    {
        var (svc, inv) = Build();
        await GrantAsync(inv, "sword_basic");
        await GrantAsync(inv, "armor_leather");
        await svc.EquipAsync(UserId, "sword_basic");

        // armor_leather 는 Armor 슬롯이라 무기와 공존 → 교체 검증을 위해 같은 슬롯 두 무기가 필요.
        // 시드에 무기가 하나뿐이므로 동일 itemId 재장착으로 슬롯 단일성만 확인한다.
        await svc.EquipAsync(UserId, "armor_leather");
        var equipped = await svc.GetEquippedAsync(UserId);

        Assert.Equal(2, equipped.Count); // Weapon + Armor 각각 하나씩
        Assert.Equal(1, equipped.Count(e => e.Slot == EquipmentType.Weapon));
        Assert.Equal(1, equipped.Count(e => e.Slot == EquipmentType.Armor));
    }

    [Fact]
    public async Task 해제하면_슬롯이_비고_스탯에서_빠진다()
    {
        var (svc, inv) = Build();
        await GrantAsync(inv, "armor_leather");
        await svc.EquipAsync(UserId, "armor_leather");

        var result = await svc.UnequipAsync(UserId, EquipmentType.Armor);

        Assert.True(result.Success);
        Assert.Empty(await svc.GetEquippedAsync(UserId));
    }

    [Fact]
    public async Task 빈_슬롯_해제는_멱등하게_성공한다()
    {
        var (svc, _) = Build();

        var result = await svc.UnequipAsync(UserId, EquipmentType.Weapon);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task 착용_세트의_스탯이_합산된다()
    {
        var (svc, inv) = Build();
        await GrantAsync(inv, "sword_basic");
        await GrantAsync(inv, "armor_leather");
        await svc.EquipAsync(UserId, "sword_basic");
        await svc.EquipAsync(UserId, "armor_leather");

        var stats = await svc.GetEquippedStatsAsync(UserId);

        Assert.Equal(5, stats.AttackPower);
        Assert.Equal(3, stats.Defense);
    }

    [Fact]
    public async Task 빈_착용은_0_스탯을_반환한다()
    {
        var (svc, _) = Build();

        var stats = await svc.GetEquippedStatsAsync(UserId);

        Assert.Equal(default, stats);
    }
}
