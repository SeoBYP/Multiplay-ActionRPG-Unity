using Script.System.GamePlayAbilitySystem;
using Server.Player;

namespace Server.Tests.Combat;

/// <summary>
/// 2.2 마나 서버 권위 서브시스템 — PlayerState 의 검증·차감·리젠·회피 게이트 단위.
/// (스킬/회피 발동 코스트 = SkillTimeline.ManaCost·DodgeConfig.ManaCost. 클라는 같은 수치로 예측.)
/// </summary>
public class PlayerStateManaTests
{
    [Fact]
    public void TrySpendMana는_충분하면_차감하고_부족하면_거부한다()
    {
        var state = new PlayerState { Mana = 100, MaxMana = 100 };

        Assert.True(state.TrySpendMana(20)); // 충분 → 차감
        Assert.Equal(80, state.Mana);

        Assert.True(state.TrySpendMana(0));  // 무료 스킬(cost 0) → 항상 통과, 변화 없음
        Assert.Equal(80, state.Mana);

        state.Mana = 10;
        Assert.False(state.TrySpendMana(15)); // 부족 → 거부
        Assert.Equal(10, state.Mana);         // 변화 없음
    }

    [Fact]
    public void RegenMana는_rate_비례로_회복하고_MaxMana로_클램프된다()
    {
        var state = new PlayerState { Mana = 50, MaxMana = 100 };

        state.RegenMana(1.0f); // 1초 × 10/s = +10
        Assert.Equal(50 + (int)ManaConfig.RegenPerSecond, state.Mana);

        state.Mana = 95;
        state.RegenMana(1.0f); // +10 이지만 상한 클램프
        Assert.Equal(100, state.Mana);

        state.RegenMana(1.0f); // 만마에서는 변화 없음
        Assert.Equal(100, state.Mana);
    }

    [Fact]
    public void RegenMana는_소수부를_누적해_정수단위로_회복한다()
    {
        var state = new PlayerState { Mana = 0, MaxMana = 100 };

        // 0.05초 × 10/s = 0.5 → 1틱으로는 정수 0(아직 회복 없음), 누적은 보존.
        state.RegenMana(0.05f);
        Assert.Equal(0, state.Mana);
        // 한 번 더 0.5 누적 → 1.0 → +1.
        state.RegenMana(0.05f);
        Assert.Equal(1, state.Mana);
    }

    [Fact]
    public void TryBeginDodge는_마나_부족이면_쿨다운도_무적도_소모하지_않고_거부된다()
    {
        var state = new PlayerState { Mana = 10, MaxMana = 100 }; // < DodgeConfig.ManaCost(15)
        const long t0 = 1_000_000;

        Assert.False(state.TryBeginDodge(t0, DodgeConfig.ManaCost)); // 마나 부족 → 거부
        Assert.Equal(10, state.Mana);                                // 미차감
        Assert.False(state.IsInvulnerableAt(t0));                    // 무적 미부여

        state.Mana = 100;
        Assert.True(state.TryBeginDodge(t0, DodgeConfig.ManaCost));  // 충분 → 발동
        Assert.Equal(100 - DodgeConfig.ManaCost, state.Mana);        // 차감
        Assert.True(state.IsInvulnerableAt(t0));                     // 무적 부여
    }
}
