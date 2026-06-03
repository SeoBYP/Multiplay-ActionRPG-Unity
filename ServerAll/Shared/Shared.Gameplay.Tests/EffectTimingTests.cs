using Script.System.GamePlayAbilitySystem;

namespace Shared.Gameplay.Tests;

/// <summary>
/// 지속시간/만료 결정론 검증. 서버·클라가 (start, duration, now)로 동일한 남은시간·만료를 계산해야 한다.
/// </summary>
public class EffectTimingTests
{
    [Fact]
    public void RemainingMs는_종료까지_남은_밀리초다()
    {
        Assert.Equal(600, EffectTiming.RemainingMs(startMs: 0, durationMs: 1000, nowMs: 400));
    }

    [Fact]
    public void 종료_시각_이후_RemainingMs는_0이다()
    {
        Assert.Equal(0, EffectTiming.RemainingMs(startMs: 0, durationMs: 1000, nowMs: 1500));
    }

    [Fact]
    public void IsExpired는_now가_종료시각_이상일때_true다()
    {
        Assert.False(EffectTiming.IsExpired(startMs: 0, durationMs: 1000, nowMs: 999, isInfinite: false));
        Assert.True(EffectTiming.IsExpired(startMs: 0, durationMs: 1000, nowMs: 1000, isInfinite: false));
    }

    [Fact]
    public void Infinite_Effect는_절대_만료되지_않는다()
    {
        Assert.False(EffectTiming.IsExpired(startMs: 0, durationMs: 1000, nowMs: 999999, isInfinite: true));
    }
}
