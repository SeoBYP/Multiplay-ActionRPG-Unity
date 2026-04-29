using System.Collections.Generic;
using System.Linq;

namespace Script.System.GamePlayAbilitySystem
{
    /// <summary>
    /// 일반 공격 Ability.
    /// 현재는 대상에게 Health 감소 GameplayEffect를 적용하는 최소 구현이다.
    /// </summary>
    public sealed class BasicAttackAbility : Ability
    {
        public int DamageAmount { get; }

        public BasicAttackAbility(int damageAmount)
        {
            DamageAmount = damageAmount;
        }

        protected override bool CanActivate(AbilityActivationContext context)
        {
            // 데미지가 있고, 자기 자신이 아닌 유효 대상이 있을 때만 공격 Ability를 실행한다.
            return base.CanActivate(context)
                   && DamageAmount > 0
                   && context?.Targets != null
                   && context.Targets.Any(IsValidTarget);
        }

        protected override void Activate(AbilityActivationContext context)
        {
            GameplayEffect damageEffect = CreateDamageEffect();

            // Ability는 대상마다 직접 Attribute를 건드리지 않고 Effect를 통해 변경을 요청한다.
            foreach (AbilitySystemComponent target in context.Targets)
            {
                if (!IsValidTarget(target))
                    continue;

                AbilitySystemUtils.ApplyEffect(target, damageEffect);
            }
        }

        private bool IsValidTarget(AbilitySystemComponent target)
        {
            return target != null && target != Owner;
        }

        private GameplayEffect CreateDamageEffect()
        {
            // 일반 공격은 Health에 음수 Additive modifier를 적용한다.
            return new GameplayEffect(new List<GameplayAttributeModifier>
            {
                new(EGameplayAttribute.Health, -DamageAmount, EModifierType.Additive)
            });
        }
    }
}
