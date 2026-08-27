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

        public void ApplyEffect(GasComponent target)
        {
            // 미보유 속성 필터링·집계는 Shared 산식이 한다(서버와 같은 경로).
            target.ApplyModifiers(Modifiers);
        }
    }
}
