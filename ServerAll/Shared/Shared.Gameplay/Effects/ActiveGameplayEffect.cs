namespace Script.System.GamePlayAbilitySystem
{
    /// <summary>
    /// 런타임에 활성 중인 Effect 인스턴스. 서버·클라가 <b>같은 타입</b>을 쓴다.
    /// 남은시간은 저장하지 않는다 — 항상 (StartMs + DurationMs) - now 로 계산한다.
    /// 그래서 남은시간을 네트워크로 흘릴 필요가 없고, 시계가 조금 어긋나도 만료 시점이 갈리지 않는다.
    ///
    /// <para><b>왜 struct 인가</b>: 생성·소멸이 잦아(피격마다 붙고 만료마다 사라진다) class 면 GC 압력이 된다.
    /// 필드 넷뿐이라 <c>List&lt;T&gt;</c> 배열에 인라인으로 들어가 개별 할당이 0 이다.</para>
    ///
    /// <para><b>왜 <c>readonly</c> 인가</b>: 가변 struct 를 컬렉션에 담으면 <c>list[i].Refresh(...)</c> 가
    /// <b>복사본을 고치고 조용히 버려진다</b>. <see cref="WithStart"/>·<see cref="WithAddedStack"/> 가 새 값을 돌려주게 해
    /// 호출부가 <c>list[i] = 새값</c> 으로 되쓰도록 강제한다.</para>
    /// </summary>
    public readonly struct ActiveGameplayEffect
    {
        public int InstanceId { get; }
        public GameplayEffectDefinition Definition { get; }
        public long StartMs { get; }
        public int Stacks { get; }

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

        /// <summary>지속시간 갱신. 새 값을 돌려주므로 호출부가 컬렉션에 되써야 한다.</summary>
        public ActiveGameplayEffect WithStart(long startMs)
            => new ActiveGameplayEffect(InstanceId, Definition, startMs, Stacks);

        /// <summary>스택 1 증가(상한 도달 시 그대로).</summary>
        public ActiveGameplayEffect WithAddedStack(int maxStacks)
            => Stacks >= maxStacks
                ? this
                : new ActiveGameplayEffect(InstanceId, Definition, StartMs, Stacks + 1);
    }
}
