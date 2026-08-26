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

    /// <summary>basic_swing(baseDamage=10) 의 데미지 모디파이어 — 구 basic_attack_dmg(-10)와 동일 값.</summary>
    private static System.Collections.Generic.List<GameplayAttributeModifier> DamageMods10()
        => global::Server.PacketHandler.Handler.CombatHandler.BuildDamageMods(
            global::Server.PacketHandler.Handler.CombatHandler.ResolveAbility(0)!, attackPower: 0, defense: 0);

    [Fact]
    public void 어빌리티_데미지는_Health_모디파이어로_만들어진다()
    {
        // AC-B 안B: 데미지 출처 = ability.baseDamage(폐기된 basic_attack_dmg effect 아님).
        var ability = global::Server.PacketHandler.Handler.CombatHandler.ResolveAbility(0)!;
        var mods = global::Server.PacketHandler.Handler.CombatHandler.BuildDamageMods(ability, attackPower: 0, defense: 0);

        var mod = Assert.Single(mods);
        Assert.Equal(EGameplayAttribute.Health, mod.AttributeType);
        Assert.Equal(EModifierType.Additive, mod.ModifierType);
        Assert.Equal(-ability.BaseDamage, mod.Amount);
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
        // (예전 자체 Dictionary 이중정의 → 단일소스 위임 회귀 가드). CC 효과로 검증 — 데미지 effect 는 AC-B B5 에서 폐기됨.
        var server = CombatEffectCatalog.Resolve("slow_3s");
        var shared = new GameplayEffectCatalog().Get("slow_3s")!.Modifiers;

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
        var id = room.Actors.Monsters()[0].InstanceId;
        var mods = DamageMods10(); // basic_swing baseDamage=10 → Health -10

        var (hit, newHp, dead) = room.Actors.DamageMonster(id, mods);

        Assert.True(hit);
        Assert.False(dead);
        Assert.Equal(30, newHp); // 40 - 10
        Assert.Equal(30, room.Actors.GetMonster(id)!.Gas[EGameplayAttribute.Health]);
    }

    [Fact]
    public void HP가_0이하면_사망처리되고_방에서_제거된다()
    {
        var room = NewRoomWithMonster();
        var id = room.Actors.Monsters()[0].InstanceId;
        var mods = DamageMods10(); // -10 each

        room.Actors.DamageMonster(id, mods); // 40 → 30
        room.Actors.DamageMonster(id, mods); // 30 → 20
        room.Actors.DamageMonster(id, mods); // 20 → 10
        var (hit, newHp, dead) = room.Actors.DamageMonster(id, mods); // 10 → 0

        Assert.True(hit);
        Assert.True(dead);
        Assert.Equal(0, newHp);
        Assert.Empty(room.Actors.Monsters());        // 제거됨
        Assert.Null(room.Actors.GetMonster(id));
    }

    [Fact]
    public void 이미_제거된_몬스터_공격은_Miss를_반환한다()
    {
        var room = NewRoomWithMonster();
        var id = room.Actors.Monsters()[0].InstanceId;
        var big = new[] { new GameplayAttributeModifier(EGameplayAttribute.Health, -999, EModifierType.Additive) };

        room.Actors.DamageMonster(id, big); // 즉사 → 제거

        var (hit, _, _) = room.Actors.DamageMonster(id, big);
        Assert.False(hit); // 이미 없음
    }
}
