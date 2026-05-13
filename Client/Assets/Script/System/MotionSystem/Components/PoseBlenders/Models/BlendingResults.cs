using Unity.Collections;
using Unity.Mathematics;

namespace Game.System.MotionSystem
{
    /// <summary>
    /// BlendBoundaries를 보간한 최종 결과 값입니다.
    /// Pose Player는 이 결과를 runtime bone Transform에 적용하게 됩니다.
    /// </summary>
    public struct BlendingResults
    {
        /// <summary>
        /// 보간이 끝난 bone position 배열입니다.
        /// </summary>
        public NativeArray<float3> bonesPosition;

        /// <summary>
        /// 보간이 끝난 bone scale 배열입니다.
        /// </summary>
        public NativeArray<float3> bonesScale;

        /// <summary>
        /// 보간이 끝난 bone rotation 배열입니다.
        /// </summary>
        public NativeArray<quaternion> bonesRotation;

        /// <summary>
        /// 보간이 끝난 root rotation입니다.
        /// </summary>
        public quaternion rootRotation;

        /// <summary>
        /// 보간이 끝난 root position입니다.
        /// </summary>
        public float3 rootPosition;
    }
}
