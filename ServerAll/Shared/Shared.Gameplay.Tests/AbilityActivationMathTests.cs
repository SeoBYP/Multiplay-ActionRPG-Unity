using System.Numerics;
using Script.System.GamePlayAbilitySystem;

namespace Shared.Gameplay.Tests;

/// <summary>
/// 어빌리티 발동 게이트(actor-combat-architecture §3) 검증. 클라 예측·서버 권위가 공유하는 순수 규칙 —
/// 쿨다운·마나·차단태그 판정과 그 우선순위(Blocked → Cooldown → Mana)를 못 박는다.
/// </summary>
public class AbilityActivationMathTests
{
    // cooldown=400, manaCost=20 인 기준 게이트. last=1000 발동 → 1400 부터 재발동 가능.
    private static AbilityActivationResult Eval(long now, long last = 1000, int cd = 400,
        int manaCost = 20, int mana = 100, bool blocked = false)
        => AbilityActivationMath.Evaluate(now, last, cd, manaCost, mana, blocked);

    [Fact]
    public void 조건을_모두_충족하면_Ok다()
    {
        Assert.Equal(AbilityActivationResult.Ok, Eval(now: 1400));
    }

    [Fact]
    public void 쿨다운이_안_지났으면_OnCooldown이다()
    {
        Assert.Equal(AbilityActivationResult.OnCooldown, Eval(now: 1399));
    }

    [Fact]
    public void 마나가_부족하면_NotEnoughMana다()
    {
        Assert.Equal(AbilityActivationResult.NotEnoughMana, Eval(now: 1400, mana: 19));
    }

    [Fact]
    public void 차단_태그가_있으면_쿨다운_마나와_무관하게_Blocked다()
    {
        // 쿨다운도 지났고 마나도 충분해도, 차단(사망·스턴)이면 최우선으로 Blocked.
        Assert.Equal(AbilityActivationResult.Blocked, Eval(now: 5000, mana: 100, blocked: true));
    }

    [Fact]
    public void 우선순위는_Blocked_다음_Cooldown_다음_Mana다()
    {
        // 차단 + 쿨다운 미경과 + 마나부족 동시 → Blocked 가 이긴다.
        Assert.Equal(AbilityActivationResult.Blocked, Eval(now: 1399, mana: 0, blocked: true));
        // 차단 아님 + 쿨다운 미경과 + 마나부족 → OnCooldown 이 마나보다 먼저.
        Assert.Equal(AbilityActivationResult.OnCooldown, Eval(now: 1399, mana: 0, blocked: false));
    }

    [Fact]
    public void CanActivate는_Ok일_때만_참이다()
    {
        Assert.True(AbilityActivationMath.CanActivate(1400, 1000, 400, 20, 100, blocked: false));
        Assert.False(AbilityActivationMath.CanActivate(1399, 1000, 400, 20, 100, blocked: false)); // 쿨다운
        Assert.False(AbilityActivationMath.CanActivate(1400, 1000, 400, 20, 0, blocked: false));   // 마나
    }

    [Fact]
    public void SkillTimeline_오버로드는_타임라인의_쿨다운과_마나코스트를_쓴다()
    {
        var skill = new SkillTimeline(
            id: "heavy_swing",
            startupMs: 400, activeMs: 100, recoveryMs: 200, cooldownMs: 1200,
            hitbox: new HitboxSpec(EHitboxShape.Box, new Vector3(0, 0, 1), new Vector3(0.5f, 0.5f, 0.5f)),
            onHitEffectIds: new[] { "basic_attack_dmg" },
            manaCost: 20);

        // last=0, cooldown=1200 → 1199 아직, 1200 부터 가능.
        Assert.Equal(AbilityActivationResult.OnCooldown,
            AbilityActivationMath.Evaluate(skill, nowMs: 1199, lastCastMs: 0, currentMana: 100, blocked: false));
        Assert.Equal(AbilityActivationResult.Ok,
            AbilityActivationMath.Evaluate(skill, nowMs: 1200, lastCastMs: 0, currentMana: 100, blocked: false));
        // manaCost=20 을 타임라인에서 읽는다.
        Assert.Equal(AbilityActivationResult.NotEnoughMana,
            AbilityActivationMath.Evaluate(skill, nowMs: 1200, lastCastMs: 0, currentMana: 19, blocked: false));
    }
}
