using UnityEngine;

namespace Game.Gameplay.Character
{
    /// <summary>
    /// MotionMatching 시스템과 캐릭터 컨트롤러 사이의 계약.
    /// GroundState가 이 인터페이스만 알면 된다 — MotionSystemV2에 직접 의존하지 않는다.
    /// null이면 기존 Animator 파라미터 방식으로 폴백된다.
    /// </summary>
    public interface IMotionMatchingDriver
    {
        /// <summary>현재 MM PlayableGraph가 재생 중인지 여부</summary>
        bool IsActive { get; }

        /// <summary>
        /// 이동 입력 기반 원하는 속도를 MM에 전달한다.
        /// FixedUpdate마다 GroundState.StateUpdate에서 호출된다.
        /// </summary>
        void SetDesiredVelocity(Vector3 worldVelocity);

        /// <summary>Ground 상태 Enter 시 MM PlayableGraph를 시작한다.</summary>
        void Resume();

        /// <summary>Ground 상태 Exit 시 MM PlayableGraph를 멈추고 AnimatorController에 제어권을 돌려준다.</summary>
        void Pause();
    }
}
