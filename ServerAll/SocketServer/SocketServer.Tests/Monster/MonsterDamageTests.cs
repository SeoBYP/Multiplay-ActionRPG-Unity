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
    private static global::Server.Room.Room NewRoomWithMonster(int hp = 30)
    {
        var room = new global::Server.Room.Room(
            1,
            new List<PlayerInfo> { new() { UserId = 1, Nickname = "A", SpawnIndex = 0 } },
            NullLogger<global::Server.Room.Room>.Instance);

        room.SpawnMonsters(
            new List<MonsterSpawnDef> { new("creepy_demon", 0f, 0f, 0f, 0f, 1, 0, Array.Empty<PatrolPoint>()) },
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
    public void CombatEffectCatalog는_Shared_단일소스를_위임한다()
    {
        // 수치 진실원은 Shared GameplayEffectCatalog 하나. 서버 접근자는 그 정의를 그대로 꺼낸다.
        // (예전 자체 Dictionary 이중정의 → 단일소스 위임 회귀 가드)
        var server = CombatEffectCatalog.Resolve("basic_attack_dmg");
        var shared = new GameplayEffectCatalog().Get("basic_attack_dmg")!.Modifiers;

        Assert.Equal(shared.Count, server.Count);
        for (int i = 0; i < shared.Count; i++)
        {
            Assert.Equal(shared[i].AttributeType, server[i].AttributeType);
            Assert.Equal(shared[i].ModifierType, server[i].ModifierType);
            Assert.Equal(shared[i].Amount, server[i].Amount);
        }
    }

    [Fact]
    public void DamageMonster는_GAS로_HP를_깎는다()
    {
        var room = NewRoomWithMonster();
        var id = room.GetAllMonsters()[0].InstanceId;
        var mods = CombatEffectCatalog.Resolve("basic_attack_dmg"); // Health -10

        var (hit, newHp, dead) = room.DamageMonster(id, mods);

        Assert.True(hit);
        Assert.False(dead);
        Assert.Equal(30, newHp); // 40 - 10
        Assert.Equal(30, room.GetMonster(id)!.Hp);
    }

    [Fact]
    public void HP가_0이하면_사망처리되고_방에서_제거된다()
    {
        var room = NewRoomWithMonster();
        var id = room.GetAllMonsters()[0].InstanceId;
        var mods = CombatEffectCatalog.Resolve("basic_attack_dmg"); // -10 each

        room.DamageMonster(id, mods); // 40 → 30
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
        var room = NewRoomWithMonster();
        var id = room.GetAllMonsters()[0].InstanceId;
        var big = new[] { new GameplayAttributeModifier(EGameplayAttribute.Health, -999, EModifierType.Additive) };

        room.DamageMonster(id, big); // 즉사 → 제거

        var (hit, _, _) = room.DamageMonster(id, big);
        Assert.False(hit); // 이미 없음
    }
}
