using UnityEngine;

namespace Game.Gameplay.Character
{
    /// <summary>
    /// 선택적 루트모션 — <b>상태 태그가 "RootMotion" 인 애니만</b> 클립의 이동을 실제 캐릭터에 적용한다.
    /// Animator 가 붙은 GameObject 에 부착한다(<c>OnAnimatorMove</c> 는 거기서만 호출된다).
    ///
    /// <b>왜 전역이 아니라 태그 게이트인가</b>(실측 근거):
    ///   · 로코모션 클립은 IPC(제자리)라 루트 이동이 0 → 켜도 영향 없음.
    ///   · 그러나 회피(Evade_*)는 클립 자체에 <b>3.6m</b>, 사망 0.84m, 기상 0.79m 의 루트 이동이 있다.
    ///     회피는 <see cref="DodgeDriver"/> 가 대시로 이동을 전담하므로 루트모션까지 먹이면 <b>이중 이동</b>이 된다.
    ///   → 그래서 "지금 이 상태가 루트모션을 쓰겠다고 선언한 경우"에만 적용한다(컨트롤러 상태 태그).
    ///
    /// 현재 태그 대상 = 공격 콤보 4단(클립 전진량 0.63~1.42m) → 스윙이 앞으로 파고든다.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public sealed class RootMotionRelay : MonoBehaviour
    {
        /// <summary>이 태그가 붙은 Animator 상태에서만 루트모션을 적용한다(컨트롤러 빌더가 부여).</summary>
        public const string RootMotionTag = "RootMotion";

        private Animator _animator;
        private CharacterMotor _motor;
        private LocomotionSettings _settings;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _motor = GetComponentInParent<CharacterMotor>();
        }

        /// <summary>중력값 주입(선택) — 미주입 시 기본 중력으로 접지만 유지한다.</summary>
        public void Construct(LocomotionSettings settings) => _settings = settings;

        private void OnAnimatorMove()
        {
            if (_animator == null || _motor == null) return;
            if (!IsRootMotionState()) return;

            // 클립의 수평 변위만 사용하고, 수직은 중력으로 접지를 유지한다(공중으로 뜨지 않게).
            Vector3 delta = _animator.deltaPosition;
            delta.y = (_settings?.Gravity ?? -15f) * Time.deltaTime;
            _motor.MoveRaw(delta);
        }

        /// <summary>전이 중이면 들어오는 상태도 본다 — 전이 시작 프레임부터 파고들어야 스윙이 끊겨 보이지 않는다.</summary>
        private bool IsRootMotionState()
        {
            if (_animator.GetCurrentAnimatorStateInfo(0).IsTag(RootMotionTag)) return true;
            return _animator.IsInTransition(0) && _animator.GetNextAnimatorStateInfo(0).IsTag(RootMotionTag);
        }
    }
}
