namespace Script.System.GamePlayAbilitySystem
{
    /// <summary>
    /// 런타임에 활성 중인 Effect 인스턴스. 서버·클라가 <b>같은 타입</b>을 쓴다.
    /// 남은시간은 저장하지 않는다 — 항상 (StartMs + DurationMs) - now 로 계산한다.
    /// 그래서 남은시간을 네트워크로 흘릴 필요가 없고, 시계가 조금 어긋나도 만료 시점이 갈리지 않는다.
    /// </summary>
    public sealed class ActiveGameplayEffect
    {
        public int InstanceId { get; }
        public GameplayEffectDefinition Definition { get; }
        public long StartMs { get; private set; }
        public int Stacks { get; private set; }

        public ActiveGameplayEffect(int instanceId, GameplayEffectDefinition definition, long startMs, int stacks)
        {
            InstanceId = instanceId;
            Definition = definition;
            StartMs = startMs;
            Stacks = stacks < 1 ? 1 : stacks;
        }

        public long EndMs => StartMs + Definition.DurationMs;
        public bool IsInfinite => Definition.Policy == EDurationPolicy.Infinite;

        /// <summary>만료 판정. 산식은 <see cref="EffectTiming"/> 단일 소스 — 서버·클라가 같은 함수를 탄다.</summary>
        public bool IsExpiredAt(long nowMs)
            => EffectTiming.IsExpired(StartMs, Definition.DurationMs, nowMs, IsInfinite);

        public void Refresh(long startMs) => StartMs = startMs;

        public void AddStack(int maxStacks)
        {
            if (Stacks < maxStacks)
                Stacks++;
        }
    }
}
