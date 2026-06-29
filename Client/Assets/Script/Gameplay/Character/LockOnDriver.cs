using Game.Gameplay.Camera;
using UnityEngine;

namespace Game.Gameplay.Character
{
    /// <summary>
    /// 락온(2.6.3) — 순수 클라 조준 보조(DodgeDriver/KnockbackDriver 형제, Action 축 보조).
    /// 토글 입력 시 화면 중앙 최근접 <see cref="LockOnTarget"/> 를 획득하고, 락 동안 매 프레임:
    ///   ① <see cref="CharacterMotor.FacingOverride"/> 로 플레이어가 타겟을 바라보게 강제(이동은 카메라기준 스트레이프 유지),
    ///   ② <see cref="CharacterCameraFollow.LockTarget"/> 로 카메라가 타겟을 프레이밍.
    /// 던전(서버 권위)은 facing → rotY → C_Move 송신으로 서버 hitbox 가 정렬되고, Main 은 LocalCombat 가 같은
    /// facing 으로 판정 → <b>패킷·서버 변경 0</b>. 타겟이 죽거나(레지스트리 이탈) 사거리를 벗어나면 자동 해제.
    /// </summary>
    public sealed class LockOnDriver
    {
        private readonly Transform _player;
        private readonly CharacterMotor _motor;
        private readonly CharacterCameraFollow _cameraFollow;
        private readonly float _maxRange;
        private readonly float _exitRangeSq; // 히스테리시스: 락 후엔 maxRange + 버퍼까지 유지(경계 떨림 방지)

        private LockOnTarget _target;
        private UnityEngine.Camera _camera;

        public bool IsLocked => _target != null;
        public LockOnTarget CurrentTarget => _target;

        public LockOnDriver(Transform player, CharacterMotor motor, CharacterCameraFollow cameraFollow, float maxRange)
        {
            _player = player;
            _motor = motor;
            _cameraFollow = cameraFollow;
            _maxRange = maxRange;
            float exit = maxRange + 3f;
            _exitRangeSq = exit * exit;
        }

        /// <summary>락온 토글: 락 중이면 해제, 아니면 화면 중앙 최근접 대상 획득.</summary>
        public void Toggle()
        {
            if (_target != null)
            {
                Debug.Log("[LockOn] 해제");
                ForceUnlock();
                return;
            }
            Acquire();
        }

        private void Acquire()
        {
            var best = LockOnTarget.FindBest(ResolveCamera(), _player.position, _maxRange);
            if (best == null)
            {
                Debug.Log("[LockOn] 락온 대상 없음(화면 안 + 사거리 내)");
                return;
            }
            _target = best;
            if (_cameraFollow != null) _cameraFollow.LockTarget = best.transform;
            Debug.Log($"[LockOn] 락온 → {best.name}");
        }

        /// <summary>락 동안 매 프레임 호출 — 유효성 검사 후 facing 강제 + 카메라 추적. 정상 흐름(생존·비스턴)에서만.</summary>
        public void Tick()
        {
            // 진짜 락 안 한 상태(C# null)면 아무것도 안 함. 단 "락했는데 대상이 파괴됨"(Unity fake-null,
            // ReferenceEquals 로 구분)은 아래 유효성 검사로 내려가 카메라/facing 을 정리해야 한다 —
            // 단순 `_target == null` 조기 반환이면 죽은 몬스터에 카메라가 영원히 잠긴다.
            if (ReferenceEquals(_target, null)) return;

            if (!IsTargetValid())
            {
                Debug.Log("[LockOn] 대상 소실/이탈 → 자동 해제");
                ForceUnlock();
                return;
            }

            Vector3 dir = _target.AimPoint - _player.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f && _motor != null)
                _motor.FacingOverride = dir.normalized;

            if (_cameraFollow != null) _cameraFollow.LockTarget = _target.transform;
        }

        private bool IsTargetValid()
        {
            if (_target == null || !_target.isActiveAndEnabled) return false; // 파괴/비활성(죽음·디스폰)
            Vector3 planar = _target.transform.position - _player.position;
            planar.y = 0f;
            return planar.sqrMagnitude <= _exitRangeSq; // 사거리(+버퍼) 이탈
        }

        /// <summary>락 강제 해제 — facing/카메라 오버라이드 원복. 사망·씬 종료 시 호출.</summary>
        public void ForceUnlock()
        {
            _target = null;
            if (_motor != null) _motor.FacingOverride = null;
            if (_cameraFollow != null) _cameraFollow.LockTarget = null;
        }

        private UnityEngine.Camera ResolveCamera()
        {
            if (_camera == null) _camera = UnityEngine.Camera.main;
            return _camera;
        }
    }
}
