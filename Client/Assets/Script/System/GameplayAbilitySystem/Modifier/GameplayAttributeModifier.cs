using System;

namespace Script.System.GamePlayAbilitySystem
{
    [Serializable]
    /// <summary>
    /// GameplayEffect가 Attribute 하나에 적용할 실제 연산 정보다.
    /// 예: Health에 -10 Additive를 적용하면 체력이 10 감소한다.
    /// </summary>
    public struct GameplayAttributeModifier
    {
        // 연산할 스텟 대상
        public EGameplayAttribute AttributeType { get; set; }

        // 추가할 연산 값
        public int Amount { get; set; }

        // 연산 방식(덧셈, 곱셈)
        public EModifierType ModifierType { get; set; }

        public GameplayAttributeModifier(EGameplayAttribute attributeType,
            int amount,
            EModifierType modifierType)
        {
            AttributeType = attributeType;
            Amount = amount;
            ModifierType = modifierType;
        }

        public static GameplayAttributeModifier Create(EGameplayAttribute attributeType, int amount,
            EModifierType modifierType)
        {
            return new GameplayAttributeModifier(attributeType, amount, modifierType);
        }
    }
}
