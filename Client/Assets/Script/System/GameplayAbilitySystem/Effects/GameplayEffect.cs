using System.Collections.Generic;

namespace Script.System.GamePlayAbilitySystem
{
    /// <summary>
    /// Attribute에 적용할 변경 묶음이다.
    /// Ability는 직접 Health를 깎지 않고 GameplayEffect를 만들어 대상 ASC에 적용한다.
    /// </summary>
    public class GameplayEffect
    {
        public List<GameplayAttributeModifier> Modifiers { get; } = new();
        
        public GameplayEffect(List<GameplayAttributeModifier> modifiers)
        {
            Modifiers = modifiers;
        }

        public void ApplyEffect(AbilitySystemComponent target)
        {
            // Effect 안의 Modifier를 순회하며 대상이 가진 Attribute에만 적용한다.
            foreach (var modifier in Modifiers)
            {
                if (target.TryGetAttribute(modifier.AttributeType, out var attribute))
                {
                    attribute.ApplyModifier(modifier);
                    continue;
                }
            }
        }
    }
}
