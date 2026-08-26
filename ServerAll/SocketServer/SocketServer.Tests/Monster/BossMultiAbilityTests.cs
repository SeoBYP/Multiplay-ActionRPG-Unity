using Microsoft.Extensions.Logging.Abstractions;
using Server.PacketHandler.Handler;
using Shared.Infrastructure.Abilities;
using Shared.Infrastructure.Messages;
using Shared.Infrastructure.Spawn;
using Shared.Packet.Packets;

namespace Server.Tests.Monster;

/// <summary>
/// AC-B B6: **보스 다중 스킬 실증** — leviathan 이 abilityIds=[leviathan_slam(강·긴쿨), leviathan_attack(평타)] 를 갖는다.
/// 코드 변경 없이 **데이터 저작만으로** 다중 스킬이 동작함을 고정한다(설계 = ability-so-authoring.md §5).
///
/// 규칙: 저작 순서 = 우선순위 → 사거리 안 + 쿨다운 경과인 **첫** 어빌리티. 쿨다운은 어빌리티 단위로 독립 추적.
/// </summary>
public class BossMultiAbilityTests
{
    private static global::Server.Room.Room NewRoom()
        => new(1,
            new List<PlayerInfo> { new() { UserId = 100, Nickname = "A", SpawnIndex = 0 } },
            NullLogger<global::Server.Room.Room>.Instance);

    /// <summary>
    /// 레비아탄을 원점에, 플레이어를 사거리 안에 두고 방을 구성한다.
    /// ※ HP 를 크게 준다 — 보스 실데미지(slam 90 + 평타 40)가 기본 HP(100)를 넘겨 **테스트 중 플레이어가 다운되면
    ///   AI 타깃에서 빠져 이후 발동이 사라지기 때문**(선택 로직이 아니라 픽스처 문제로 실패한다).
    /// </summary>
    private static global::Server.Room.Room NewBossRoom(float playerX = 1f)
    {
        var room = NewRoom();
        room.AddPlayer(100, "A", 0, playerX, 0f, 0f, 0f, attackPower: 0, defense: 0, maxHealth: 100_000);
        room.MarkJoined(100);
        room.SpawnMonsters(
            new List<MonsterSpawnDef> { new("leviathan", 0f, 0f, 0f, 0f, 1, 0, Array.Empty<PatrolPoint>()) },
            new MapBounds(0f, 0f, 400f, 400f));
        return room;
    }

    [Fact]
    public void 보스는_어빌리티_2개를_저작으로_갖는다()
    {
        var abilities = global::Server.Monster.MonsterCatalog.GetAbilities("leviathan");

        Assert.Equal(2, abilities.Count);
        Assert.Equal("leviathan_slam", abilities[0].Id);   // 우선순위 1 = 강스킬
        Assert.Equal("leviathan_attack", abilities[1].Id); // 우선순위 2 = 평타
    }

    [Fact]
    public void 공격_사거리는_어빌리티들의_최대값으로_파생된다()
    {
        // slam(3.5) > attack(3.0) → AI 는 3.5 부터 Attack 페이즈로 멈춘다.
        var stats = global::Server.Monster.MonsterCatalog.Get("leviathan");
        var abilities = global::Server.Monster.MonsterCatalog.GetAbilities("leviathan");

        Assert.Equal(abilities.Max(a => a.ActivationRange), stats.AttackRange);
        Assert.Equal(AbilityCatalog.Get("leviathan_slam")!.ActivationRange, stats.AttackRange);
    }

    [Fact]
    public void 첫_발동은_우선순위가_높은_강스킬이다()
    {
        var room = NewBossRoom();

        var act = room.Tick(0.1f, 1_000_000).OfType<S_AbilityActivated>().Single();

        Assert.Equal(AbilityCatalog.Get("leviathan_slam")!.NetworkId, act.SkillId);
    }

    [Fact]
    public void 강스킬_쿨다운_중에는_평타로_폴백한다()
    {
        // B6 의 핵심: abilityIds 순서만으로 "강스킬 → 쿨다운이면 평타" 패턴이 성립한다(코드 변경 0).
        var room = NewBossRoom();
        var slam = AbilityCatalog.Get("leviathan_slam")!;
        var attack = AbilityCatalog.Get("leviathan_attack")!;
        const long t0 = 1_000_000;

        var first = room.Tick(0.1f, t0).OfType<S_AbilityActivated>().Single();
        Assert.Equal(slam.NetworkId, first.SkillId); // 1) 강스킬

        // slam 쿨다운(6000) 중이지만 평타 쿨다운(1800)은 지남 → 평타 발동.
        var second = room.Tick(0.1f, t0 + attack.Timeline.CooldownMs).OfType<S_AbilityActivated>().Single();
        Assert.Equal(attack.NetworkId, second.SkillId); // 2) 평타 폴백
    }

    [Fact]
    public void 강스킬_쿨다운이_끝나면_다시_강스킬을_쓴다()
    {
        var room = NewBossRoom();
        var slam = AbilityCatalog.Get("leviathan_slam")!;
        const long t0 = 1_000_000;

        room.Tick(0.1f, t0); // slam 발동 → 쿨다운 시작

        // slam 쿨다운 경과 시점 → 우선순위대로 다시 slam.
        var again = room.Tick(0.1f, t0 + slam.Timeline.CooldownMs).OfType<S_AbilityActivated>().Single();
        Assert.Equal(slam.NetworkId, again.SkillId);
    }

    [Fact]
    public void 쿨다운은_어빌리티마다_독립_추적된다()
    {
        // 평타를 여러 번 쓰는 동안에도 slam 쿨다운은 자기 시계로 흐른다(단일 LastAttackAt 이었다면 불가능).
        var room = NewBossRoom();
        var slam = AbilityCatalog.Get("leviathan_slam")!;
        var attack = AbilityCatalog.Get("leviathan_attack")!;
        const long t0 = 1_000_000;

        room.Tick(0.1f, t0);                                   // slam
        room.Tick(0.1f, t0 + attack.Timeline.CooldownMs);      // 평타
        room.Tick(0.1f, t0 + attack.Timeline.CooldownMs * 2);  // 평타

        // slam 쿨다운이 지나면, 평타를 그 사이 몇 번 썼든 slam 이 다시 최우선.
        var act = room.Tick(0.1f, t0 + slam.Timeline.CooldownMs).OfType<S_AbilityActivated>().Single();
        Assert.Equal(slam.NetworkId, act.SkillId);
    }

    [Fact]
    public void 강스킬은_평타보다_강하고_CC도_어빌리티_저작값을_따른다()
    {
        var room = NewBossRoom();
        var slam = AbilityCatalog.Get("leviathan_slam")!;

        var effects = room.Tick(0.1f, 1_000_000).OfType<S_ApplyEffect>().ToList();

        // 데미지 = slam.baseDamage(90) — 평타(40)보다 강하다. Defense 0 → base 그대로.
        var dmg = effects.Single(e => e.EffectId == CombatHandler.AbilityDamageEffectId);
        Assert.Equal(-slam.BaseDamage, dmg.Amount);
        Assert.True(slam.BaseDamage > AbilityCatalog.Get("leviathan_attack")!.BaseDamage);

        // CC = slam 이 저작한 stun(평타는 slow) → 어빌리티마다 다른 CC 가 나간다.
        var cc = effects.Single(e => e.EffectId == "stun_1_5s");
        Assert.Equal(0, cc.Amount);
    }

    [Fact]
    public void 평타_사거리_밖_강스킬_사거리_안이면_강스킬만_발동한다()
    {
        // 사거리 판정도 어빌리티 단위 — slam(3.5) 안 / attack(3.0) 밖인 거리에서는 slam 만 후보가 된다.
        var room = NewBossRoom(playerX: 3.2f);
        var slam = AbilityCatalog.Get("leviathan_slam")!;
        const long t0 = 1_000_000;

        var first = room.Tick(0.1f, t0).OfType<S_AbilityActivated>().Single();
        Assert.Equal(slam.NetworkId, first.SkillId);

        // slam 쿨다운 중 + 평타는 사거리 밖 → 아무것도 발동하지 않는다(접근만).
        Assert.Empty(room.Tick(0.1f, t0 + 2000).OfType<S_AbilityActivated>());
    }
}
