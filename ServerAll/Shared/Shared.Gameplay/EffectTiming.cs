namespace Script.System.GamePlayAbilitySystem
{
    /// <summary>
    /// 지속시간/만료 순수 함수. 서버·클라가 (start, duration, now)로 동일하게 남은시간·만료를 계산한다.
    /// 남은시간을 네트워크로 주고받지 않고, 권위 startMs + 공유 정의 durationMs로 각자 산출한다.
    /// </summary>
    public static class EffectTiming
    {
        public static int RemainingMs(long startMs, int durationMs, long nowMs)
        {
            long remaining = startMs + durationMs - nowMs;
            if (remaining < 0) remaining = 0;
            return (int)remaining;
        }

        public static bool IsExpired(long startMs, int durationMs, long nowMs, bool isInfinite)
        {
            if (isInfinite)
                return false;
            return nowMs >= startMs + durationMs;
        }
    }
}
