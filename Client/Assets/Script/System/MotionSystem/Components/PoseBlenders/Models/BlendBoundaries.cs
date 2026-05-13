using Unity.Collections;
using Unity.Mathematics;

namespace Game.System.MotionSystem
{
    /// <summary>
    /// Pose blending의 시작 값과 목표 값을 저장하는 경계 데이터입니다.
    /// 새 pose가 선택될 때 현재 pose를 start로, 선택된 database pose를 end로 저장하고
    /// blend 시간 동안 두 값 사이를 보간합니다.
    /// </summary>
    public struct BlendBoundaries
    {
        /// <summary>
        /// blend 시작 시점의 bone rotation 배열입니다.
        /// </summary>
        public NativeArray<quaternion> startRotationValues;

        /// <summary>
        /// blend 목표 bone rotation 배열입니다.
        /// </summary>
        public NativeArray<quaternion> endRotationValues;

        /// <summary>
        /// blend 시작 시점의 bone position 배열입니다.
        /// </summary>
        public NativeArray<float3> startPositionValues;

        /// <summary>
        /// blend 목표 bone position 배열입니다.
        /// </summary>
        public NativeArray<float3> endPositionValues;

        /// <summary>
        /// blend 시작 시점의 bone scale 배열입니다.
        /// </summary>
        public NativeArray<float3> startScaleValues;

        /// <summary>
        /// blend 목표 bone scale 배열입니다.
        /// </summary>
        public NativeArray<float3> endScaleValues;

        /// <summary>
        /// blend 시작 시점의 root position입니다.
        /// </summary>
        public float3 startRootPositionToBlend;

        /// <summary>
        /// blend 목표 root position입니다.
        /// </summary>
        public float3 endRootPositionToBlend;

        /// <summary>
        /// blend 시작 시점의 root rotation입니다.
        /// </summary>
        public quaternion startRootRotationToBlend;

        /// <summary>
        /// blend 목표 root rotation입니다.
        /// </summary>
        public quaternion endRootRotationToBlend;
    }
}
