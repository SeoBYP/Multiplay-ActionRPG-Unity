using Microsoft.Extensions.Logging.Abstractions;
using Script.System.GamePlayAbilitySystem;
using Server.Combat;
using Shared.Infrastructure.Messages;
using Shared.Infrastructure.Spawn;

namespace Server.Tests.Monster;

/// <summary>
/// M3 증분⑤: 서버 권위 몬스터 피격/사망(GAS). Room.DamageMonster 가 GameplayEffectMath 로 HP 를 깎고,
/// 0 이하면 방에서 제거한다. CombatEffectCatalog 가 effectId 를 Health 모디파이어로 해석한다.
/// </summary>
public class MonsterDamageTests
{
    private static global::Server.Room.Room NewRoomWithSlime(int hp = 30)
    {
        var room = new global::Server.Room.Room(
            1,
            new List<PlayerInfo> { new() { UserId = 1, Nickname = "A", SpawnIndex = 0 } },
            NullLogger<global::Server.Room.Room>.Instance);

        room.SpawnMonsters(
            new List<MonsterSpawnDef> { new("slime", 0f, 0f, 0f, 0f, 1, 0, Array.Empty<PatrolPoint>()) },
            new MapBounds(0f, 0f, 40f, 40f));
        return room;
    }

    [Fact]
    public void CombatEffectCatalog는_basic_attack_dmg를_Health모디파이어로_해석한다()
    {
        var mods = CombatEffectCatalog.Resolve("basic_attack_dmg");

        var mod = Assert.Single(mods);
        Assert.Equal(EGameplayAttribute.Health, mod.AttributeType);
        Assert.Equal(EModifierType.Additive, mod.ModifierType);
        Assert.Equal(-10, mod.Amount);
    }

    [Fact]
    public void 미등록_효과는_빈_모디파이어를_반환한다()
    {
        Assert.Empty(CombatEffectCatalog.Resolve("no_such_effect"));
    }

    [Fact]
    public void DamageMonster는_GAS로_HP를_깎는다()
    {
        var room = NewRoomWithSlime();
        var id = room.GetAllMonsters()[0].InstanceId;
        var mods = CombatEffectCatalog.Resolve("basic_attack_dmg"); // Health -10

        var (hit, newHp, dead) = room.DamageMonster(id, mods);

        Assert.True(hit);
        Assert.False(dead);
        Assert.Equal(20, newHp); // 30 - 10
        Assert.Equal(20, room.GetMonster(id)!.Hp);
    }

    [Fact]
    public void HP가_0이하면_사망처리되고_방에서_제거된다()
    {
        var room = NewRoomWithSlime();
        var id = room.GetAllMonsters()[0].InstanceId;
        var mods = CombatEffectCatalog.Resolve("basic_attack_dmg"); // -10 each

        room.DamageMonster(id, mods); // 30 → 20
        room.DamageMonster(id, mods); // 20 → 10
        var (hit, newHp, dead) = room.DamageMonster(id, mods); // 10 → 0

        Assert.True(hit);
        Assert.True(dead);
        Assert.Equal(0, newHp);
        Assert.Empty(room.GetAllMonsters());        // 제거됨
        Assert.Null(room.GetMonster(id));
    }

    [Fact]
    public void 이미_제거된_몬스터_공격은_Miss를_반환한다()
    {
        var room = NewRoomWithSlime();
        var id = room.GetAllMonsters()[0].InstanceId;
        var big = new[] { new GameplayAttributeModifier(EGameplayAttribute.Health, -999, EModifierType.Additive) };

        room.DamageMonster(id, big); // 즉사 → 제거

        var (hit, _, _) = room.DamageMonster(id, big);
        Assert.False(hit); // 이미 없음
    }
}
