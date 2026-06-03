using System.Collections.Generic;

namespace Script.System.GamePlayAbilitySystem
{
    /// <summary>
    /// Effect의 정적 정의(불변). **표시 데이터(Sprite/색)를 포함하지 않는다** — 서버와 공유 가능.
    /// 1단계는 코드 카탈로그로 생성, 2단계에서 공유 JSON 로더가 같은 형태로 채운다.
    /// </summary>
    public sealed class GameplayEffectDefinition
    {
        public string Id { get; }
        public EEffectCategory Category { get; }
        public EDurationPolicy Policy { get; }
        public int DurationMs { get; }
        public EStackPolicy Stack { get; }
        public int MaxStacks { get; }
        public IReadOnlyList<GameplayAttributeModifier> Modifiers { get; }

        /// <summary>버프/디버프 색 판정. null이면 modifier 부호로 자동 판정한다.</summary>
        public bool? PolarityOverride { get; }

        public GameplayEffectDefinition(
            string id,
            EEffectCategory category,
            EDurationPolicy policy,
            int durationMs,
            IReadOnlyList<GameplayAttributeModifier> modifiers,
            EStackPolicy stack = EStackPolicy.None,
            int maxStacks = 1,
            bool? polarityOverride = null)
        {
            Id = id;
            Category = category;
            Policy = policy;
            DurationMs = durationMs;
            Modifiers = modifiers ?? new List<GameplayAttributeModifier>();
            Stack = stack;
            MaxStacks = maxStacks < 1 ? 1 : maxStacks;
            PolarityOverride = polarityOverride;
        }
    }
}
