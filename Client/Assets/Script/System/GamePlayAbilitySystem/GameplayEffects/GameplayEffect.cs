using System.Collections.Generic;

namespace Script.System.GamePlayAbilitySystem
{
    public class GameplayEffect
    {
        public List<GameplayAttributeModifier> Modifiers { get; } = new();
        
        public GameplayEffect(List<GameplayAttributeModifier> modifiers)
        {
            Modifiers = modifiers;
        }

        public void ApplyEffect(AbilitySystemComponent target)
        {
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