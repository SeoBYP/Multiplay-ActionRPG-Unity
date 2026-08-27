using System;
using UnityEngine;

namespace Script.System.GamePlayAbilitySystem
{
    [Serializable]
    /// <summary>
    /// 속성의 <b>저작(authoring) 값</b>. Inspector/프리팹에 직렬화되는 시작값일 뿐, 런타임 상태가 아니다.
    ///
    /// <para><b>왜 값이 여기 살지 않는가</b>: 런타임 속성은 Shared <see cref="AbilitySystemComponent"/> 가 소유한다.
    /// 예전에는 여기(클라)와 서버가 각자 상태를 들고 각자 산식을 돌려서, 같은 Effect 를 걸어도
    /// 스택 처리·만료 시점이 갈릴 수 있었다. 저작은 엔진의 일이고 계산은 공유의 일이라 그렇게 갈랐다.</para>
    ///
    /// <para>필드 이름을 바꾸지 말 것 — 프리팹 YAML 이 이 이름으로 바인딩된다
    /// (PlayerCharacter·RemotePlayerCharacter 등에 이미 저작된 값이 있다).</para>
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

        /// <summary>시작 현재값(저작). 런타임 현재값은 <see cref="GasComponent.Current"/> 로 읽는다.</summary>
        public int StartingValue => _currentValue;

        public GameplayAttribute(EGameplayAttribute attributeType, int baseValue, int maxValue,
            EAttributeKind kind = EAttributeKind.Resource)
        {
            _attributeType = attributeType;
            _kind = kind;
            _baseValue = baseValue;
            _maxValue = maxValue;
            _currentValue = Mathf.Clamp(baseValue, 0, maxValue);
        }

        /// <summary>Inspector 에서 잘못된 값이 들어와도 런타임 초기화가 깨지지 않게 보정한다.</summary>
        public void Validate()
        {
            _maxValue = Mathf.Max(0, _maxValue);
            _baseValue = Mathf.Clamp(_baseValue, 0, _maxValue);
            _currentValue = Mathf.Clamp(_currentValue, 0, _maxValue);
        }
    }
}
