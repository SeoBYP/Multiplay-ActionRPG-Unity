using Server.Player;

namespace Server.Tests.Combat;

/// <summary>
/// ⓓ 서버 발동 게이트(권위 쿨다운). PlayerState.TryBeginSkill 가 C_Attack 연사=폭딜 치팅을 차단한다.
/// CombatHandler.HandleAttack 가 이 게이트를 통과한 발동만 적중 판정으로 넘긴다.
/// </summary>
public class SkillCooldownGateTests
{
    private const int SkillId = 0;
    private const int CooldownMs = 400;

    [Fact]
    public void 첫_발동은_항상_허용된다()
    {
        var p = new PlayerState { UserId = 1 };
        Assert.True(p.TryBeginSkill(SkillId, CooldownMs, nowMs: 1000));
    }

    [Fact]
    public void 쿨다운_안지났으면_연사는_거부된다()
    {
        var p = new PlayerState { UserId = 1 };

        Assert.True(p.TryBeginSkill(SkillId, CooldownMs, nowMs: 1000));
        Assert.False(p.TryBeginSkill(SkillId, CooldownMs, nowMs: 1100)); // 100ms 뒤 — 거부
        Assert.False(p.TryBeginSkill(SkillId, CooldownMs, nowMs: 1399)); // 경계 직전 — 거부
    }

    [Fact]
    public void 쿨다운_지나면_다시_발동된다()
    {
        var p = new PlayerState { UserId = 1 };

        Assert.True(p.TryBeginSkill(SkillId, CooldownMs, nowMs: 1000));
        Assert.True(p.TryBeginSkill(SkillId, CooldownMs, nowMs: 1400)); // 경계 — 허용, 기준 갱신
        Assert.False(p.TryBeginSkill(SkillId, CooldownMs, nowMs: 1500)); // 갱신된 기준 기준 쿨다운 중
    }

    [Fact]
    public void 스킬별로_쿨다운이_독립_추적된다()
    {
        var p = new PlayerState { UserId = 1 };

        Assert.True(p.TryBeginSkill(0, CooldownMs, nowMs: 1000));
        Assert.True(p.TryBeginSkill(1, CooldownMs, nowMs: 1000)); // 다른 스킬 — 독립
        Assert.False(p.TryBeginSkill(0, CooldownMs, nowMs: 1100));
    }
}
