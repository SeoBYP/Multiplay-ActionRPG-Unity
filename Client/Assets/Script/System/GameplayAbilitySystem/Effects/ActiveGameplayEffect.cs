namespace Script.System.GamePlayAbilitySystem
{
    /// <summary>
    /// 런타임에 활성 중인 Effect 인스턴스. tick·(2단계)동기화 대상.
    /// 남은시간은 저장하지 않는다 — 항상 (StartMs + DurationMs) - now 로 계산.
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

        public void Refresh(long startMs) => StartMs = startMs;

        public void AddStack(int maxStacks)
        {
            if (Stacks < maxStacks)
                Stacks++;
        }
    }
}
