using UnityEngine;

namespace Game.Gameplay.Character
{
    /// <summary>
    /// Animator 가 붙은 GameObject(모델 루트)에 부착. Unity Animation Event 는 Animator GO 의 컴포넌트만
    /// 호출할 수 있으므로, AttackA 클립의 이벤트(AttackHitStart/AttackHitEnd)를 자식 <see cref="WeaponHitbox"/>로 릴레이한다.
    /// </summary>
    public sealed class WeaponAnimationEventRelay : MonoBehaviour
    {
        private WeaponHitbox _hitbox;

        private void Awake() => _hitbox = GetComponentInChildren<WeaponHitbox>(true);

        // Animation Event functionName 규약 — AttackA1hMelee 클립 이벤트와 이름이 일치해야 한다.
        public void AttackHitStart()
        {
            if (_hitbox == null) _hitbox = GetComponentInChildren<WeaponHitbox>(true);
            _hitbox?.ActivateWindow();
        }

        public void AttackHitEnd() => _hitbox?.DeactivateWindow();
    }
}
