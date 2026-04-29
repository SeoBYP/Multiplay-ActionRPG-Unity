using System;
using UnityEngine;

namespace Script.System.GamePlayAbilitySystem
{
    [Serializable]
    /// <summary>
    /// Health, Mana 같은 단일 스탯 값이다.
    /// Unity 직렬화를 위해 field는 SerializeField로 보관하고 외부에는 읽기 전용으로 노출한다.
    /// </summary>
    public class GameplayAttribute
    {
        [SerializeField] private EGameplayAttribute _attributeType;
        [SerializeField] private int _baseValue;
        [SerializeField] private int _maxValue;
        [SerializeField] private int _currentValue;

        public EGameplayAttribute AttributeType => _attributeType;
        public int BaseValue => _baseValue;
        public int MaxValue => _maxValue;
        public int CurrentValue => _currentValue;

        public GameplayAttribute(EGameplayAttribute attributeType, int baseValue, int maxValue)
        {
            _attributeType = attributeType;
            _baseValue = baseValue;
            _maxValue = maxValue;
            _currentValue = Mathf.Clamp(baseValue, 0, maxValue);
        }

        public void ApplyModifier(GameplayAttributeModifier modifier)
        {
            // 현재 값은 항상 0~MaxValue 범위 안에 머물도록 clamp한다.
            switch (modifier.ModifierType)
            {
                case EModifierType.Additive:
                    _currentValue = Mathf.Clamp(_currentValue + modifier.Amount, 0, _maxValue);
                    break;
                case EModifierType.Multiplicative:
                    _currentValue = Mathf.Clamp(_currentValue * modifier.Amount / 100, 0, _maxValue);
                    break;
            }
        }

        public void Validate()
        {
            // Inspector에서 잘못된 값이 들어와도 런타임 Attribute 범위가 깨지지 않게 보정한다.
            _maxValue = Mathf.Max(0, _maxValue);
            _baseValue = Mathf.Clamp(_baseValue, 0, _maxValue);
            _currentValue = Mathf.Clamp(_currentValue, 0, _maxValue);
        }
    }
}
