using Server.Actors;

namespace Server.Tests.Combat;

/// <summary>
/// ⓓ 서버 발동 게이트(권위 쿨다운). Actor 의 GAS 쿨다운(TryBeginAbility)이 C_Attack 연사=폭딜 치팅을 차단한다.
/// CombatHandler.HandleAttack 가 이 게이트를 통과한 발동만 적중 판정으로 넘긴다.
/// </summary>
public class SkillCooldownGateTests
{
    private const string SkillId = "basic_swing";
    private const int CooldownMs = 400;

    [Fact]
    public void 첫_발동은_항상_허용된다()
    {
        var p = new PlayerActor(1);
        Assert.True(p.Gas.TryBeginAbility(SkillId, CooldownMs, nowMs: 1000));
    }

    [Fact]
    public void 쿨다운_안지났으면_연사는_거부된다()
    {
        var p = new PlayerActor(1);

        Assert.True(p.Gas.TryBeginAbility(SkillId, CooldownMs, nowMs: 1000));
        Assert.False(p.Gas.TryBeginAbility(SkillId, CooldownMs, nowMs: 1100)); // 100ms 뒤 — 거부
        Assert.False(p.Gas.TryBeginAbility(SkillId, CooldownMs, nowMs: 1399)); // 경계 직전 — 거부
    }

    [Fact]
    public void 쿨다운_지나면_다시_발동된다()
    {
        var p = new PlayerActor(1);

        Assert.True(p.Gas.TryBeginAbility(SkillId, CooldownMs, nowMs: 1000));
        Assert.True(p.Gas.TryBeginAbility(SkillId, CooldownMs, nowMs: 1400)); // 경계 — 허용, 기준 갱신
        Assert.False(p.Gas.TryBeginAbility(SkillId, CooldownMs, nowMs: 1500)); // 갱신된 기준 기준 쿨다운 중
    }

    [Fact]
    public void 스킬별로_쿨다운이_독립_추적된다()
    {
        var p = new PlayerActor(1);

        Assert.True(p.Gas.TryBeginAbility("skill_a", CooldownMs, nowMs: 1000));
        Assert.True(p.Gas.TryBeginAbility("skill_b", CooldownMs, nowMs: 1000)); // 다른 스킬 — 독립
        Assert.False(p.Gas.TryBeginAbility("skill_a", CooldownMs, nowMs: 1100));
    }
}
