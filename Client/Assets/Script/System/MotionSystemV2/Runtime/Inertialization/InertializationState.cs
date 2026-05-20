using UnityEngine;

namespace Script.System.MotionSystemV2
{
    /// <summary>
    /// Inertialization 블렌드 상태를 추적하는 값 타입.
    /// 포즈 전환 시 BlendWeight가 1(이전 포즈)에서 0(새 포즈)으로 Cubic Hermite 곡선을 따라 감쇠한다.
    ///
    /// Cubic Hermite 선택 이유:
    ///   시작·끝 모두 velocity = 0 → 전환 초기/종료 시 가속도 없음 → 팝 현상 방지
    ///   f(u) = (1-u)²(1+2u)  (u = elapsed/duration)
    /// </summary>
    public struct InertializationState
    {
        private float _duration;
        private float _elapsed;
        private bool  _active;

        public bool IsActive => _active;

        /// <summary>
        /// 이전 포즈 혼합 비율. 1에서 시작해 0으로 감쇠.
        /// inactive 상태에서는 항상 0 (완전히 새 포즈).
        /// </summary>
        public float BlendWeight
        {
            get
            {
                if (!_active) return 0f;
                float u = Mathf.Clamp01(_elapsed / _duration);
                return (1f - u) * (1f - u) * (1f + 2f * u);
            }
        }

        /// <summary>
        /// 새 블렌드를 시작한다. 이미 진행 중이면 남은 잔여에서 재시작한다.
        /// </summary>
        public void Begin(float duration)
        {
            _duration = Mathf.Max(0.01f, duration);
            _elapsed  = 0f;
            _active   = true;
        }

        /// <summary>
        /// FixedUpdate마다 호출. duration 경과 시 자동 비활성화.
        /// </summary>
        public void Tick(float dt)
        {
            if (!_active) return;
            _elapsed += dt;
            if (_elapsed >= _duration) _active = false;
        }
    }
}
