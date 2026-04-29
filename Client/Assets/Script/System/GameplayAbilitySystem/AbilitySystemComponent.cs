using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Script.System.GamePlayAbilitySystem
{
    /// <summary>
    /// 캐릭터나 오브젝트가 GAS 기능을 사용하기 위한 진입점이다.
    /// Attribute를 보관하고, Ability를 부여하고, Ability 활성화를 중계한다.
    /// </summary>
    public class AbilitySystemComponent : MonoBehaviour
    {
        // Inspector에서 초기 Attribute 값을 세팅한다. 런타임 조회는 Dictionary로 한다.
        public List<GameplayAttribute> Attributes = new();

        private Dictionary<EGameplayAttribute, GameplayAttribute> _gameplayAttributes = new();
        private readonly List<Ability> _abilities = new();
        private bool _initialized;

        public IReadOnlyList<Ability> Abilities => _abilities;

        private void Awake()
        {
            InitializeAttributes();
        }

        public void InitializeAttributes()
        {
            if (Attributes.Count == 0)
            {
                // 테스트용 또는 세팅 누락 방지용 기본값. 실제 캐릭터는 Inspector에서 명시하는 것이 좋다.
                Attributes.Add(new GameplayAttribute(EGameplayAttribute.Health, 100, 100));
            }

            _gameplayAttributes.Clear();
            foreach (var attribute in Attributes)
            {
                attribute.Validate();
                _gameplayAttributes[attribute.AttributeType] = attribute;
            }

            _initialized = true;
        }

        private void OnValidate()
        {
            foreach (var attribute in Attributes)
            {
                attribute.Validate();
            }
        }
        
        public GameplayAttribute GetAttribute(EGameplayAttribute attributeType)
        {
            EnsureInitialized();
            return _gameplayAttributes.GetValueOrDefault(attributeType);
        }

        public bool TryGetAttribute(EGameplayAttribute attributeType, out GameplayAttribute attribute)
        {
            EnsureInitialized();
            return _gameplayAttributes.TryGetValue(attributeType, out attribute);
        }

        public T GrantAbility<T>(T ability) where T : Ability
        {
            if (ability == null)
                throw new ArgumentNullException(nameof(ability));

            // Ability가 어떤 ASC에 속하는지 연결한 뒤 목록에 보관한다.
            ability.GrantTo(this);
            _abilities.Add(ability);
            return ability;
        }

        public bool TryActivateAbility<T>(AbilityActivationContext context) where T : Ability
        {
            // 타입으로 Ability를 찾는 편의 API. 같은 타입 Ability를 여러 개 둘 계획이면 별도 핸들이 필요하다.
            T ability = _abilities.OfType<T>().FirstOrDefault();
            return ability != null && TryActivateAbility(ability, context);
        }

        public bool TryActivateAbility(Ability ability, AbilityActivationContext context)
        {
            if (ability == null)
                return false;

            // 호출자가 Source를 몰라도 ASC가 항상 자신을 Source로 넣어준다.
            AbilityActivationContext sourcedContext = (context ?? new AbilityActivationContext(new List<AbilitySystemComponent>()))
                .WithSource(this);
            return ability.TryActivate(sourcedContext);
        }

        private void EnsureInitialized()
        {
            if (!_initialized)
                InitializeAttributes();
        }
    }
}
