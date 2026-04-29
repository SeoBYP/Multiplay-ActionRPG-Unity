using System;
using UnityEngine;

namespace Script.System.GamePlayAbilitySystem
{
    [Serializable]
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
            _maxValue = Mathf.Max(0, _maxValue);
            _baseValue = Mathf.Clamp(_baseValue, 0, _maxValue);
            _currentValue = Mathf.Clamp(_currentValue, 0, _maxValue);
        }
    }
}
