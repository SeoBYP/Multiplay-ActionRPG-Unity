using System;

namespace Script.System.GamePlayAbilitySystem
{
    /// <summary>
    /// GameplayEffect가 Attribute 하나에 적용할 연산 정보.
    /// Multiplicative.Amount는 퍼센트(예: 120 = ×1.2).
    /// 클라이언트(Unity)와 서버가 공유하는 단일 타입 — Unity 직렬화 호환을 위해 [Serializable] + settable.
    /// </summary>
    [Serializable]
    public struct GameplayAttributeModifier
    {
        public EGameplayAttribute AttributeType { get; set; }
        public int Amount { get; set; }
        public EModifierType ModifierType { get; set; }

        public GameplayAttributeModifier(EGameplayAttribute attributeType, int amount, EModifierType modifierType)
        {
            AttributeType = attributeType;
            Amount = amount;
            ModifierType = modifierType;
        }

        public static GameplayAttributeModifier Create(EGameplayAttribute attributeType, int amount, EModifierType modifierType)
            => new GameplayAttributeModifier(attributeType, amount, modifierType);
    }
}
