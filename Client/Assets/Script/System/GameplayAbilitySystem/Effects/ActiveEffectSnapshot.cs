namespace Script.System.GamePlayAbilitySystem
{
    /// <summary>
    /// 표시/중계용 활성 Effect 스냅샷. ASC가 내부 clock 기준으로 남은시간을 계산해 만든다.
    /// (clock을 ASC 밖으로 노출하지 않기 위함)
    /// </summary>
    public readonly struct ActiveEffectSnapshot
    {
        public readonly string EffectId;
        public readonly int RemainingMs;
        public readonly int DurationMs;
        public readonly int Stacks;
        public readonly bool IsInfinite;

        public ActiveEffectSnapshot(string effectId, int remainingMs, int durationMs, int stacks, bool isInfinite)
        {
            EffectId = effectId;
            RemainingMs = remainingMs;
            DurationMs = durationMs;
            Stacks = stacks;
            IsInfinite = isInfinite;
        }
    }
}
