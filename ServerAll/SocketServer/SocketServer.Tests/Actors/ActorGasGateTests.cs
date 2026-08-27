using Microsoft.Extensions.Logging.Abstractions;
using Script.System.GamePlayAbilitySystem;
using Server.Actors;
using Shared.Infrastructure.Messages;
using Shared.Infrastructure.Spawn;
using Shared.Packet.Packets;

namespace Server.Tests.Actors;

/// <summary>
/// Actor 통합으로 <b>새로 가능해진 것</b>을 고정한다.
///
/// 예전에는 서버가 태그를 상태로 갖지 않아 발동 게이트에 <c>blocked: false</c> 가 하드코딩돼 있었다
/// — 서버가 자기가 뿌린 CC 를 스스로 모르는 상태였다. 이제 게이트가 액터의 GAS 태그를 읽으므로,
/// 스턴/사망 태그가 붙은 액터는 종족과 무관하게 발동이 막힌다.
/// </summary>
public class ActorGasGateTests
{
    private static global::Server.Room.Room NewRoom()
        => new(1,
            new List<PlayerInfo> { new() { UserId = 100, Nickname = "A", SpawnIndex = 0 } },
            NullLogger<global::Server.Room.Room>.Instance);

    private static global::Server.Room.Room RoomWithMonsterInRange()
    {
        var room = NewRoom();
        room.AddPlayer(100, "A", 0, 0.5f, 0f, 0f, 0f);
        room.MarkJoined(100);
        room.SpawnMonsters(
            new List<MonsterSpawnDef> { new("creepy_demon", 0f, 0f, 0f, 0f, 1, 0, Array.Empty<PatrolPoint>()) },
            new MapBounds(0f, 0f, 40f, 40f));
        return room;
    }

    [Fact]
    public void 스턴_태그가_붙은_몬스터는_사거리_안이어도_발동하지_않는다()
    {
        var room = RoomWithMonsterInRange();
        room.Actors.GetMonster(1)!.Gas.AddTag(GameplayTags.Stun);

        var packets = room.Tick(0.1f, 1_000_000);

        Assert.Empty(packets.OfType<S_AbilityActivated>());
        Assert.Empty(packets.OfType<S_ApplyEffect>());
    }

    [Fact]
    public void 스턴이_풀리면_다시_발동한다()
    {
        var room = RoomWithMonsterInRange();
        var monster = room.Actors.GetMonster(1)!;
        monster.Gas.AddTag(GameplayTags.Stun);
        Assert.Empty(room.Tick(0.1f, 1_000_000).OfType<S_AbilityActivated>());

        monster.Gas.RemoveTag(GameplayTags.Stun);

        Assert.Single(room.Tick(0.1f, 1_000_100).OfType<S_AbilityActivated>());
    }

    [Fact]
    public void 쿨다운은_종족과_무관하게_같은_저장소에서_추적된다()
    {
        // 플레이어와 몬스터가 같은 AbilitySystemComponent API 를 쓴다 — 예전엔 키 타입이 int/string 으로 갈려 있었다.
        Actor player = new PlayerActor(100);
        Actor monster = new MonsterActor(1) { MonsterId = "creepy_demon" };

        foreach (var actor in new[] { player, monster })
        {
            Assert.True(actor.Gas.TryBeginAbility("swing", cooldownMs: 400, nowMs: 1000));
            Assert.False(actor.Gas.TryBeginAbility("swing", cooldownMs: 400, nowMs: 1399));
            Assert.True(actor.Gas.TryBeginAbility("swing", cooldownMs: 400, nowMs: 1400));
            Assert.True(actor.Gas.TryBeginAbility("other", cooldownMs: 400, nowMs: 1400)); // 어빌리티별 독립
        }
    }

    [Fact]
    public void 몬스터는_공격력_방어력_스탯을_0으로_가진게_아니라_아예_없다()
    {
        // 예전엔 이 0 이 산식 호출부의 const(MonsterAttackPower/MonsterDefense)였다.
        // 이제는 "미보유"가 데이터로 표현된다 — 스탯이 생기면 스폰 시 Define 만 하면 된다.
        var room = RoomWithMonsterInRange();
        var monster = room.Actors.GetMonster(1)!;

        Assert.False(monster.Gas.Has(EGameplayAttribute.AttackPower));
        Assert.False(monster.Gas.Has(EGameplayAttribute.Defense));
        Assert.False(monster.Gas.Has(EGameplayAttribute.Mana));
        Assert.True(monster.Gas.Has(EGameplayAttribute.Health)); // 이건 모든 액터가 갖는다
    }

    [Fact]
    public void 플레이어는_스탯이_0이어도_보유한다()
    {
        // 스탯 인자를 생략한 경로(레거시/테스트)도 "0 인 스탯"으로 부여된다 — 몬스터의 미보유와 구분된다.
        var room = NewRoom();
        room.AddPlayer(100, "A", 0, 0f, 0f, 0f, 0f); // attackPower/defense 생략

        var gas = room.Actors.GetMember(100)!.Actor.Gas;
        Assert.True(gas.Has(EGameplayAttribute.AttackPower));
        Assert.Equal(0, gas[EGameplayAttribute.AttackPower]);
    }

    [Fact]
    public void 서버가_Health_이외_속성_효과도_적용한다_Defense()
    {
        // 회귀 가드: 예전 AbilitySystemComponent 는 Health 만 필터해 def_down_10 같은 효과를 조용히 버렸다.
        // Defense 는 서버 데미지 산식의 입력이라, 클라만 적용하면 두 쪽 데미지가 갈린다.
        var room = NewRoom();
        room.AddPlayer(100, "A", 0, 0f, 0f, 0f, 0f, attackPower: 0, defense: 8);

        room.Actors.ApplyEffect(ActorIds.FromPlayer(100), new[]
        {
            GameplayAttributeModifier.Create(EGameplayAttribute.Defense, -5, EModifierType.Additive),
        });

        Assert.Equal(3, room.Actors.GetMember(100)!.Actor.Gas[EGameplayAttribute.Defense]);
    }
}
