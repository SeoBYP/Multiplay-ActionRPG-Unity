using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Character
{
    /// <summary>
    /// 무기 프롭(WeaponProp)에 부착. 휘두르는 활성 구간에만 트리거 콜라이더로 LocalMonster 적중을 감지한다.
    /// 활성 구간은 AttackA 클립의 Animation Event(→ <see cref="WeaponAnimationEventRelay"/>)가 여닫는다.
    /// 스윙당 같은 대상은 1회만 적중(_hitThisSwing). 데미지 산식/적용은 구독자(LocalCombat)가 수행.
    ///
    /// Main(클라 권위) 전용 연출·판정. 던전(서버 권위)은 서버 HitboxMath 를 유지하며 이 콜라이더를 쓰지 않는다.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class WeaponHitbox : MonoBehaviour
    {
        private Collider _collider;
        private bool _active;
        private readonly HashSet<LocalMonster> _hitThisSwing = new();

        /// <summary>활성 구간 중 새 LocalMonster 최초 접촉 시 발행(스윙당 대상 1회).</summary>
        public event Action<LocalMonster> OnHit;

        private void Awake()
        {
            _collider = GetComponent<Collider>();
            _collider.isTrigger = true;
            _collider.enabled = false; // 평상시 꺼둔다 — 활성 구간에만 켠다.
        }

        /// <summary>Animation Event(타격 시작 프레임) → 활성 구간 시작. 이전 스윙 적중기록 초기화.</summary>
        public void ActivateWindow()
        {
            _hitThisSwing.Clear();
            _active = true;
            if (_collider != null) _collider.enabled = true;
        }

        /// <summary>Animation Event(타격 끝 프레임) → 활성 구간 종료.</summary>
        public void DeactivateWindow()
        {
            _active = false;
            if (_collider != null) _collider.enabled = false;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_active) return;
            var monster = other.GetComponentInParent<LocalMonster>();
            if (monster == null || monster.IsDead || !_hitThisSwing.Add(monster)) return;
            OnHit?.Invoke(monster);
        }
    }
}
