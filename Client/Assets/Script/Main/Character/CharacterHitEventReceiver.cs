using System.Collections.Generic;
using Script.System.GamePlayAbilitySystem;
using UnityEngine;

namespace Game.Main.Character
{
    public class CharacterHitEventReceiver : MonoBehaviour
    {
        [SerializeField] private HitDetector _hitDetector;
        [SerializeField] private int _damage = 10;

        private readonly HashSet<AbilitySystemComponent> _hitTargets = new();
        private bool _hitWindowOpen;

        private void Awake()
        {
            _hitDetector ??= GetComponent<HitDetector>();
        }

        public void ResetHitTargets()
        {
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
            if (!_hitWindowOpen)
                _hitWindowOpen = true;

            if (_hitDetector == null)
            {
                Debug.LogWarning($"{nameof(CharacterHitEventReceiver)} requires a {nameof(HitDetector)}.", this);
                return;
            }

            IReadOnlyList<AbilitySystemComponent> targets = _hitDetector.PerformDetection();
            foreach (AbilitySystemComponent target in targets)
            {
                if (!_hitTargets.Add(target))
                    continue;

                ApplyDamage(target);
            }
        }

        private void ApplyDamage(AbilitySystemComponent target)
        {
            GameplayEffect damageEffect = new(new List<GameplayAttributeModifier>
            {
                new(EGameplayAttribute.Health, -_damage, EModifierType.Additive)
            });

            AbilitySystemUtils.ApplyEffect(target, damageEffect);
        }
    }
}
