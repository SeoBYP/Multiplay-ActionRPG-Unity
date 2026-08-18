using GameServer.Domain.Entities.Equipment;
using GameServer.Domain.Entities.Inventory;
using Shared.Gameplay.Equipment;
using Shared.Infrastructure.Items;

namespace GameServer.Tests.Domain.Entities;

public class EquipmentCatalogTests
{
    [Fact]
    public void 시드_장비는_슬롯과_스탯_모디파이어를_가진다()
    {
        var sword = EquipmentCatalog.Get("sword_basic");
        Assert.NotNull(sword);
        Assert.Equal(EquipmentType.Weapon, sword!.Slot);
        Assert.Equal(5, sword.Stats.AttackPower);

        var armor = EquipmentCatalog.Get("armor_leather");
        Assert.NotNull(armor);
        Assert.Equal(EquipmentType.Armor, armor!.Slot);
        Assert.Equal(3, armor.Stats.Defense);
    }

    [Fact]
    public void 장비가_아닌_아이템은_IsEquippable이_false다()
    {
        Assert.False(EquipmentCatalog.IsEquippable("potion_hp_small"));
        Assert.Null(EquipmentCatalog.Get("potion_hp_small"));
    }

    [Fact]
    public void 모든_장비_정의는_ItemCatalog에도_소유_메타가_있어야_한다()
    {
        // 장착은 소유(InventoryItem→ItemCatalog) 경로를 거치므로 두 카탈로그가 동기화돼 있어야 한다.
        foreach (var def in EquipmentCatalog.All)
        {
            var item = ItemCatalog.Get(def.ItemId);
            Assert.NotNull(item);
            Assert.False(item!.Stackable); // 장비는 스택형 1칸(개별 인스턴스 없음)
            Assert.Equal(1, item.MaxStack);
        }
    }

    [Fact]
    public void StatModifier_Add는_항목별로_합산한다()
    {
        var a = new EquipmentStatModifier(AttackPower: 5, Defense: 1);
        var b = new EquipmentStatModifier(Defense: 3, Strength: 2);

        var sum = a.Add(b);

        Assert.Equal(5, sum.AttackPower);
        Assert.Equal(4, sum.Defense);
        Assert.Equal(2, sum.Strength);
        Assert.Equal(0, sum.Intelligence);
    }
}
