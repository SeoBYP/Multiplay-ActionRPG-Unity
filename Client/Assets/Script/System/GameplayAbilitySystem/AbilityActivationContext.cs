using System.Collections.Generic;

namespace Script.System.GamePlayAbilitySystem
{
    /// <summary>
    /// Ability 한 번을 실행할 때 필요한 런타임 정보다.
    /// 지금은 Source와 Targets만 있지만, 나중에 HitPoint, 방향, 태그, 서버 판정 정보 등을 추가할 수 있다.
    /// </summary>
    public sealed class AbilityActivationContext
    {
        public AbilitySystemComponent Source { get; }
        public IReadOnlyList<AbilitySystemComponent> Targets { get; }

        public AbilityActivationContext(IReadOnlyList<AbilitySystemComponent> targets)
            : this(null, targets)
        {
        }

        private AbilityActivationContext(AbilitySystemComponent source, IReadOnlyList<AbilitySystemComponent> targets)
        {
            Source = source;
            Targets = targets ?? new List<AbilitySystemComponent>();
        }

        public AbilityActivationContext WithSource(AbilitySystemComponent source)
        {
            // ASC가 Ability를 실행할 때 자기 자신을 Source로 주입한다.
            return new AbilityActivationContext(source, Targets);
        }
    }
}
