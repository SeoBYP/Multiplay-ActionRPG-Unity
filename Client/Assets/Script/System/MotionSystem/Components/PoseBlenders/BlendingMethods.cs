using Unity.Mathematics;
using UnityEngine;

namespace Game.System.MotionSystem
{
    /// <summary>
    /// Pose blending 단계에서 사용할 보간 함수 모음입니다.
    /// Animator CrossFade를 사용하지 않는 Pose Player 구조에서는 선택된 pose를 즉시 적용하면 snap이 생기므로,
    /// 이전 pose와 다음 pose 사이의 위치/회전 값을 직접 보간해야 합니다.
    /// </summary>
    public static class BlendingMethods
    {
        /// <summary>
        /// quaternion을 정규화 선형 보간합니다.
        /// Slerp보다 저렴하지만 큰 회전 차이에서는 회전 궤적이 덜 정확할 수 있습니다.
        /// </summary>
        public static quaternion Lerp(quaternion startValue, quaternion endValue, float elapsedTime, float lerpDuration)
        {
            return math.nlerp(startValue, endValue, elapsedTime / lerpDuration);
        }

        /// <summary>
        /// float3 값을 선형 보간합니다.
        /// bone localPosition, scale, root position 보간에 사용할 수 있습니다.
        /// </summary>
        public static float3 Lerp(float3 startValue, float3 endValue, float elapsedTime, float lerpDuration)
        {
            return math.lerp(startValue, endValue, elapsedTime / lerpDuration);
        }

        /// <summary>
        /// quaternion을 구면 선형 보간합니다.
        /// bone rotation이나 root rotation처럼 회전 경로가 중요한 값에 사용합니다.
        /// </summary>
        public static quaternion SLerp(quaternion startValue, quaternion endValue, float elapsedTime, float lerpDuration)
        {
            return math.slerp(startValue, endValue, elapsedTime / lerpDuration);
        }

        /// <summary>
        /// float3 방향 벡터를 구면 선형 보간합니다.
        /// 위치 값보다는 방향 벡터나 궤적 방향 보간에 적합합니다.
        /// </summary>
        public static float3 SLerp(float3 startValue, float3 endValue, float elapsedTime, float lerpDuration)
        {
            return Vector3.Slerp(startValue, endValue, elapsedTime / lerpDuration);
        }
    }
}
