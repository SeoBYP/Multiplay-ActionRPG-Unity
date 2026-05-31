using System.Collections.Generic;
using Script.System.GamePlayAbilitySystem;
using UnityEngine;

namespace Game.Gameplay.Character
{
    /// <summary>
    /// 공격 애니메이션 이벤트와 GAS Ability를 연결하는 어댑터다.
    /// Animation Event가 PerformHit를 호출하면 HitDetector로 대상을 찾고 BasicAttackAbility를 활성화한다.
    /// </summary>
    public class CharacterHitEventReceiver : MonoBehaviour
    {
        [SerializeField] private HitDetector _hitDetector;
        [SerializeField] private int _damage = 10;

        private readonly HashSet<AbilitySystemComponent> _hitTargets = new();
        private AbilitySystemComponent _abilitySystem;
        private BasicAttackAbility _basicAttackAbility;
        private bool _hitWindowOpen;

        private void Awake()
        {
            _hitDetector ??= GetComponent<HitDetector>();
            // 무기나 하위 오브젝트에 붙어 있어도 캐릭터 루트 ASC를 찾을 수 있게 Parent까지 탐색한다.
            _abilitySystem = GetComponentInParent<AbilitySystemComponent>();

            if (_abilitySystem != null)
                _basicAttackAbility = _abilitySystem.GrantAbility(new BasicAttackAbility(_damage));
        }

        public void ResetHitTargets()
        {
            // 공격 1회마다 중복 타격 방지 목록을 초기화한다.
            _hitTargets.Clear();
            _hitWindowOpen = false;
        }

        public void BeginHitWindow()
        {
            _hitWindowOpen = true;
        }
        
        public void EndHitWindow()
        {
            _hitWindowOpen = false;
        }
        
        public void PerformHit()
        {
            // 현재는 Animation Event 하나만 붙여도 동작하도록 자동으로 hit window를 연다.
            if (!_hitWindowOpen)
                _hitWindowOpen = true;

            if (_hitDetector == null)
            {
                Debug.LogWarning($"{nameof(CharacterHitEventReceiver)} requires a {nameof(HitDetector)}.", this);
                return;
            }

            List<AbilitySystemComponent> newTargets = new();
            IReadOnlyList<AbilitySystemComponent> targets = _hitDetector.PerformDetection();
            foreach (AbilitySystemComponent target in targets)
            {
                // 같은 공격 동작 안에서 이미 맞은 대상은 다시 데미지를 받지 않는다.
                if (!_hitTargets.Add(target))
                    continue;

                newTargets.Add(target);
            }

            if (newTargets.Count == 0)
                return;

            if (_abilitySystem == null || _basicAttackAbility == null)
            {
                Debug.LogWarning($"{nameof(CharacterHitEventReceiver)} requires an {nameof(AbilitySystemComponent)}.", this);
                return;
            }

            // Hit 판정 결과를 Ability activation context로 넘겨 GAS 흐름을 시작한다.
            _abilitySystem.TryActivateAbility(_basicAttackAbility, new AbilityActivationContext(newTargets));
        }
    }
}
