using Script.System.GamePlayAbilitySystem;
using UnityEngine;

namespace Game.Gameplay.Character
{
    /// <summary>
    /// 피격 타격감용 per-actor HitStop — 잠깐 Animator.speed=0 (전역 Time.timeScale 금지, 설계문서 §5).
    /// 자신 HP 감소(피격) 시 자동 트리거 + 공격자측에서 Begin() 직접 호출(연출 예측).
    /// </summary>
    public class HitStopController : MonoBehaviour
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private float _defaultDurationSec = 0.08f;

        private AbilitySystemComponent _asc;
        private int _lastHealth = int.MinValue;
        private float _restoreAt = -1f;

        private void Awake()
        {
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
            _asc = GetComponentInParent<AbilitySystemComponent>();
        }

        private void OnEnable()
        {
            if (_asc != null) _asc.OnAttributeChanged += OnAttributeChanged;
        }

        private void OnDisable()
        {
            if (_asc != null) _asc.OnAttributeChanged -= OnAttributeChanged;
        }

        private void OnAttributeChanged(EGameplayAttribute type, int current, int max)
        {
            if (type != EGameplayAttribute.Health) return;
            if (_lastHealth != int.MinValue && current < _lastHealth)
                Begin(); // 피격(HP 감소) → HitStop
            _lastHealth = current;
        }

        /// <summary>HitStop 시작. duration 생략 시 기본값.</summary>
        public void Begin(float? durationSec = null)
        {
            if (_animator == null) return;
            _animator.speed = 0f;
            _restoreAt = Time.unscaledTime + (durationSec ?? _defaultDurationSec);
        }

        private void Update()
        {
            if (_restoreAt < 0f) return;
            if (Time.unscaledTime < _restoreAt) return;

            if (_animator != null) _animator.speed = 1f;
            _restoreAt = -1f;
        }
    }
}
