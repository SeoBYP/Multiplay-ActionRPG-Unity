using System.Numerics;
using Script.System.GamePlayAbilitySystem;

namespace Shared.Gameplay.Tests;

/// <summary>
/// CA-2: SkillTimeline 페이즈 진행(startup→active→recovery→done) 결정론 검증.
/// active window는 서버가 애니 없이 적중을 평가하는 구간 — 데이터로만 결정된다.
/// </summary>
public class SkillTimelineTests
{
    private static SkillTimeline Make() => new SkillTimeline(
        id: "basic_swing",
        startupMs: 200, activeMs: 100, recoveryMs: 150, cooldownMs: 500,
        hitbox: new HitboxSpec(EHitboxShape.Box, new Vector3(0, 0, 1), new Vector3(0.5f, 0.5f, 0.5f)),
        onHitEffectIds: new[] { "basic_attack_dmg" });

    [Fact]
    public void 페이즈는_startup_active_recovery_done_순으로_진행된다()
    {
        var t = Make();
        Assert.Equal(ESkillPhase.Startup,  SkillTimelineMath.PhaseAt(t, 0));
        Assert.Equal(ESkillPhase.Startup,  SkillTimelineMath.PhaseAt(t, 199));
        Assert.Equal(ESkillPhase.Active,   SkillTimelineMath.PhaseAt(t, 200));
        Assert.Equal(ESkillPhase.Active,   SkillTimelineMath.PhaseAt(t, 299));
        Assert.Equal(ESkillPhase.Recovery, SkillTimelineMath.PhaseAt(t, 300));
        Assert.Equal(ESkillPhase.Recovery, SkillTimelineMath.PhaseAt(t, 449));
        Assert.Equal(ESkillPhase.Done,     SkillTimelineMath.PhaseAt(t, 450));
    }

    [Fact]
    public void IsActive는_active_window_안에서만_참이다()
    {
        var t = Make();
        Assert.False(SkillTimelineMath.IsActive(t, 199));
        Assert.True(SkillTimelineMath.IsActive(t, 200));
        Assert.True(SkillTimelineMath.IsActive(t, 299));
        Assert.False(SkillTimelineMath.IsActive(t, 300));
    }

    [Fact]
    public void 페이즈_경계와_총길이가_데이터로_계산된다()
    {
        var t = Make();
        Assert.Equal(200, t.ActiveStartMs);
        Assert.Equal(300, t.ActiveEndMs);
        Assert.Equal(450, t.TotalMs);
    }
}
