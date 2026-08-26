using Microsoft.Extensions.Logging.Abstractions;
using Script.System.GamePlayAbilitySystem;
using Shared.Infrastructure.Messages;
using Shared.Infrastructure.Spawn;
using Shared.Packet.Packets;

using Server.PacketHandler.Handler;

namespace Server.Tests.Monster;

/// <summary>
/// M3 ⑤b + AC: 몬스터→플레이어 공격. Attack 페이즈 + 발동 게이트(AbilityActivationMath) 통과 시 최근접 플레이어에
/// ability_damage(S_ApplyEffect, 수치=ability.BaseDamage)를 발행하고, 통합 발동 연출 신호 S_AbilityActivated(ActorId=-instanceId)를 함께 브로드캐스트한다.
/// </summary>
public class MonsterAttackTests
{
    private static global::Server.Room.Room NewRoom()
        => new(1,
            new List<PlayerInfo> { new() { UserId = 100, Nickname = "A", SpawnIndex = 0 } },
            NullLogger<global::Server.Room.Room>.Instance);

    [Fact]
    public void 몬스터가_사거리_안_플레이어를_쿨다운마다_공격한다()
    {
        var room = NewRoom();
        room.AddPlayer(100, "A", 0, 0.5f, 0f, 0f, 0f); // 플레이어를 몬스터(0,0,0) 사거리 안에
        room.MarkJoined(100);                                // 입장 완료 = 라이브 타깃
        room.SpawnMonsters(
            new List<MonsterSpawnDef> { new("creepy_demon", 0f, 0f, 0f, 0f, 1, 0, Array.Empty<PatrolPoint>()) },
            new MapBounds(0f, 0f, 40f, 40f));

        const long t0 = 1_000_000; // LastAttackAt=0 이므로 첫 틱은 즉시 공격

        var p1 = room.Tick(0.1f, t0);
        // creepy_demon 은 데미지(ability_damage) 단일 효과(CC 없음). CC 부여 검증은 arachnya 테스트가 담당.
        var atk1 = p1.OfType<S_ApplyEffect>().Single(e => e.EffectId == CombatHandler.AbilityDamageEffectId);
        Assert.Equal(100, atk1.TargetId);
        Assert.Equal(ActorIds.FromMonster(1), atk1.SourceId); // AC: 몬스터 = -instanceId(첫 스폰=1)

        // 즉시 다시 틱 → 쿨다운(1500ms) 내라 공격 없음
        var p2 = room.Tick(0.1f, t0 + 100);
        Assert.Empty(p2.OfType<S_ApplyEffect>());

        // 쿨다운 경과 후 → 다시 공격(데미지 패킷 1개)
        var p3 = room.Tick(0.1f, t0 + 2000);
        Assert.Single(p3.OfType<S_ApplyEffect>().Where(e => e.EffectId == CombatHandler.AbilityDamageEffectId));
    }

    [Fact]
    public void 몬스터_공격은_플레이어_Defense를_빼고_데미지를_적용한다()
    {
        var room = NewRoom();
        // creepy_demon AttackDamage=12, 플레이어 Defense=2 → 데미지 = max(1, 12-2) = 10
        room.AddPlayer(100, "A", 0, 0.5f, 0f, 0f, 0f, attackPower: 0, defense: 2);
        room.MarkJoined(100);
        room.SpawnMonsters(
            new List<MonsterSpawnDef> { new("creepy_demon", 0f, 0f, 0f, 0f, 1, 0, Array.Empty<PatrolPoint>()) },
            new MapBounds(0f, 0f, 40f, 40f));

        var atk = room.Tick(0.1f, 1_000_000).OfType<S_ApplyEffect>().Single(e => e.EffectId == CombatHandler.AbilityDamageEffectId);
        Assert.Equal(-10, atk.Amount); // 서버 권위 Health 델타(Defense 반영)

        // 서버 HP 도 같은 값으로 차감(클라 표시값 == 서버 권위).
        var hp = room.Actors.Members().Single().Actor.Gas[EGameplayAttribute.Health];
        Assert.Equal(global::Server.Room.Room.DefaultMaxHp - 10, hp);
    }

    [Fact]
    public void Defense가_공격력보다_커도_최소_1_데미지는_들어간다()
    {
        var room = NewRoom();
        // creepy_demon AttackDamage=12, 플레이어 Defense=20 → max(1, 12-20) = 1 (무피해 방지)
        room.AddPlayer(100, "A", 0, 0.5f, 0f, 0f, 0f, attackPower: 0, defense: 20);
        room.MarkJoined(100);
        room.SpawnMonsters(
            new List<MonsterSpawnDef> { new("creepy_demon", 0f, 0f, 0f, 0f, 1, 0, Array.Empty<PatrolPoint>()) },
            new MapBounds(0f, 0f, 40f, 40f));

        var atk = room.Tick(0.1f, 1_000_000).OfType<S_ApplyEffect>().Single(e => e.EffectId == CombatHandler.AbilityDamageEffectId);
        Assert.Equal(-1, atk.Amount);
        Assert.Equal(global::Server.Room.Room.DefaultMaxHp - 1, room.Actors.Members().Single().Actor.Gas[EGameplayAttribute.Health]);
    }

    [Fact]
    public void 아라크냐_공격은_슬로우_CC를_함께_브로드캐스트한다()
    {
        // CC 부여 몬스터로 arachnya(monsters.json onHitEffectId=slow_3s) 사용 — creepy_demon 은 CC 없음.
        var room = NewRoom();
        room.AddPlayer(100, "A", 0, 0.5f, 0f, 0f, 0f); // 사거리 안
        room.MarkJoined(100);
        room.SpawnMonsters(
            new List<MonsterSpawnDef> { new("arachnya", 0f, 0f, 0f, 0f, 1, 0, Array.Empty<PatrolPoint>()) },
            new MapBounds(0f, 0f, 40f, 40f));

        var effects = room.Tick(0.1f, 1_000_000).OfType<S_ApplyEffect>().ToList();

        // 데미지 + CC(slow_3s, monsters.json) 두 효과를 함께 브로드캐스트.
        var cc = effects.Single(e => e.EffectId == "slow_3s");
        Assert.Equal(100, cc.TargetId);
        Assert.Equal(0, cc.Amount); // CC = HP 변경 없는 상태태그(GrantedTags)
    }

    [Fact]
    public void 다운된_플레이어는_몬스터_공격_대상에서_제외된다()
    {
        var room = NewRoom();
        room.AddPlayer(100, "A", 0, 0.5f, 0f, 0f, 0f); // 사거리 안
        room.MarkJoined(100);
        room.SpawnMonsters(
            new List<MonsterSpawnDef> { new("creepy_demon", 0f, 0f, 0f, 0f, 1, 0, Array.Empty<PatrolPoint>()) },
            new MapBounds(0f, 0f, 40f, 40f));

        // 살아있을 때: 공격 발생(데미지 패킷)
        Assert.Single(room.Tick(0.1f, 1_000_000).OfType<S_ApplyEffect>().Where(e => e.EffectId == CombatHandler.AbilityDamageEffectId));

        // 실제로 HP 를 0 으로 만든다 → State.Dead 태그가 붙어 타깃에서 빠진다 → 쿨다운 지나도 공격 없음.
        // (만피인 채로 TryMarkFailed 만 부르던 예전 방식은 이제 거부된다 — 다운도 서버 권위다.)
        room.Progress.ApplyPlayerEffect(100, new[]
        {
            GameplayAttributeModifier.Create(EGameplayAttribute.Health, -9999, EModifierType.Additive),
        });
        Assert.True(room.Actors.GetMember(100)!.Actor.Gas.HasTag(GameplayTags.Dead));

        Assert.Empty(room.Tick(0.1f, 1_000_000 + 5000).OfType<S_ApplyEffect>());
    }

    [Fact]
    public void 몬스터_공격시_S_AbilityActivated_발동신호를_함께_브로드캐스트한다()
    {
        // AC: 데미지(S_ApplyEffect)와 별개로 "이 액터가 스킬을 썼다" 통합 신호 → 클라 ActorRegistry 가 스윙 애니 재생.
        var room = NewRoom();
        room.AddPlayer(100, "A", 0, 0.5f, 0f, 0f, 0f);
        room.MarkJoined(100);
        room.SpawnMonsters(
            new List<MonsterSpawnDef> { new("creepy_demon", 0f, 0f, 0f, 0f, 1, 0, Array.Empty<PatrolPoint>()) },
            new MapBounds(0f, 0f, 40f, 40f));

        var act = room.Tick(0.1f, 1_000_000).OfType<S_AbilityActivated>().Single();

        Assert.Equal(ActorIds.FromMonster(1), act.ActorId); // 몬스터 = -1(첫 스폰)
        Assert.True(ActorIds.IsMonster(act.ActorId));
        // AC-B B4: 몬스터 공격도 어빌리티 → SkillId = creepy_demon_attack 의 networkId(데이터 저작값).
        Assert.Equal(Shared.Infrastructure.Abilities.AbilityCatalog.Get("creepy_demon_attack")!.NetworkId, act.SkillId);
    }

    [Fact]
    public void 무적_플레이어에게도_S_AbilityActivated_헛스윙_신호는_나간다()
    {
        // i-frame 으로 데미지는 빗나가도(S_ApplyEffect 없음) 스윙 애니 신호는 나가야 한다(발동 broadcast 를 무적 continue 앞에 둔 이유).
        var room = NewRoom();
        room.AddPlayer(100, "A", 0, 0.5f, 0f, 0f, 0f);
        room.MarkJoined(100);
        room.SpawnMonsters(
            new List<MonsterSpawnDef> { new("creepy_demon", 0f, 0f, 0f, 0f, 1, 0, Array.Empty<PatrolPoint>()) },
            new MapBounds(0f, 0f, 40f, 40f));

        const long t0 = 1_000_000;
        Assert.True(room.Actors.GetMember(100)!.Actor.TryBeginDodge(t0)); // 무적 창 부여

        var packets = room.Tick(0.1f, t0 + 100);

        Assert.Empty(packets.OfType<S_ApplyEffect>());       // 데미지는 빗나감(무적)
        Assert.Single(packets.OfType<S_AbilityActivated>()); // 그래도 헛스윙 발동 신호는 나감
    }

    [Fact]
    public void 플레이어가_aggro밖이면_공격하지_않는다()
    {
        var room = NewRoom();
        room.AddPlayer(100, "A", 0, 100f, 0f, 0f, 0f); // 멀리(aggro 밖)
        room.MarkJoined(100);                                // 입장은 했지만 사거리 밖
        room.SpawnMonsters(
            new List<MonsterSpawnDef> { new("creepy_demon", 0f, 0f, 0f, 0f, 1, 0, Array.Empty<PatrolPoint>()) },
            new MapBounds(0f, 0f, 400f, 400f));

        var packets = room.Tick(0.1f, 1_000_000);

        Assert.Empty(packets.OfType<S_ApplyEffect>());   // 공격 없음
        Assert.NotEmpty(packets.OfType<S_MonsterState>()); // 상태 브로드캐스트는 여전히 함
    }
}
