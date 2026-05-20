using Script.System.MotionSystemV2;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Main.Character
{
    /// <summary>
    /// IMotionMatchingDriver 구현체. MotionMatchingComponent MonoBehaviour를 래핑한다.
    /// 캐릭터 GameObject에 MotionMatchingComponent와 함께 부착한다.
    ///
    /// 역할 분리:
    ///   MotionMatchingComponent — 포즈 검색, Playables 구동, Inertialization (MotionSystemV2 레이어)
    ///   MotionMatchingDriver    — 컨트롤러와의 연결, Resume/Pause 조율 (Main 레이어)
    /// </summary>
    [RequireComponent(typeof(MotionMatchingComponent))]
    public class MotionMatchingDriver : MonoBehaviour, IMotionMatchingDriver
    {
        private MotionMatchingComponent _mm;

        private void Awake()
        {
            _mm = GetComponent<MotionMatchingComponent>();
        }

        public bool IsActive => _mm != null && _mm.IsActive;

        public void SetDesiredVelocity(Vector3 worldVelocity)
        {
            if (_mm != null)
                _mm.DesiredVelocity = (float3)worldVelocity;
        }

        public void Resume()
        {
            _mm?.Resume();
        }

        public void Pause()
        {
            _mm?.Pause();
        }
    }
}
