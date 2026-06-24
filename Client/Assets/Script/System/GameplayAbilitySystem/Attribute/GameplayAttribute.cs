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
        [SerializeField] private EAttributeKind _kind = EAttributeKind.Resource;
        [SerializeField] private int _baseValue;
        [SerializeField] private int _maxValue;
        [SerializeField] private int _currentValue;

        public EGameplayAttribute AttributeType => _attributeType;
        public EAttributeKind Kind => _kind;
        public int BaseValue => _baseValue;
        public int MaxValue => _maxValue;
        public int CurrentValue => _currentValue;

        /// <summary>CurrentValue가 실제로 변할 때만 발행. 구독자: ASC가 모아서 외부로 재발행.</summary>
        public event Action OnChanged;

        public GameplayAttribute(EGameplayAttribute attributeType, int baseValue, int maxValue,
            EAttributeKind kind = EAttributeKind.Resource)
        {
            _attributeType = attributeType;
            _kind = kind;
            _baseValue = baseValue;
            _maxValue = maxValue;
            _currentValue = Mathf.Clamp(baseValue, 0, maxValue);
        }

        /// <summary>
        /// Stat 재계산 결과를 반영한다 (Base + 활성 modifier의 파생값).
        /// ASC.RecalculateStats만 호출. 값이 실제로 변하면 OnChanged 발행.
        /// </summary>
        public void SetCurrent(int value)
        {
            int clamped = Mathf.Clamp(value, 0, _maxValue);
            if (clamped == _currentValue)
                return;
            _currentValue = clamped;
            OnChanged?.Invoke();
        }

        /// <summary>
        /// 권위 스탯(레벨 파생 MaxHealth 등)으로 Base/Max 를 재설정한다.
        /// Current 는 새 Max 로 클램프만(초과분 제거) — 풀충전이 필요하면 호출 측이 SetCurrent(max) 를 잇는다.
        /// 클라 prefab 기준선(예 HP 100)을 서버 권위값으로 정렬할 때 사용.
        /// </summary>
        public void SetMax(int maxValue)
        {
            _maxValue = Mathf.Max(0, maxValue);
            _baseValue = Mathf.Clamp(_baseValue, 0, _maxValue);
            SetCurrent(Mathf.Min(_currentValue, _maxValue)); // 초과 클램프 + 실제 변화 시 OnChanged
        }

        public void ApplyModifier(GameplayAttributeModifier modifier)
        {
            // 현재 값은 항상 0~MaxValue 범위 안에 머물도록 clamp한다.
            int previous = _currentValue;
            switch (modifier.ModifierType)
            {
                case EModifierType.Additive:
                    _currentValue = Mathf.Clamp(_currentValue + modifier.Amount, 0, _maxValue);
                    break;
                case EModifierType.Multiplicative:
                    _currentValue = Mathf.Clamp(_currentValue * modifier.Amount / 100, 0, _maxValue);
                    break;
            }

            if (_currentValue != previous)
                OnChanged?.Invoke();
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
