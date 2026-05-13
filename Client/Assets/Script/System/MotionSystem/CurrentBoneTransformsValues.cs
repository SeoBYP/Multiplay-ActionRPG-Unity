using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Game.System.MotionSystem
{
    /// <summary>
    /// 현재 runtime 캐릭터 skeleton의 Transform 값을 NativeArray로 캐시하는 구조체입니다.
    /// Pose search는 database pose뿐 아니라 현재 캐릭터 pose/velocity와도 비교해야 하므로,
    /// 매 frame TransformAccessArray job으로 이 값을 갱신하는 구조로 확장할 수 있습니다.
    /// </summary>
    public struct CurrentBoneTransformsValues
    {
        /// <summary>
        /// 현재 bone world position 배열입니다.
        /// </summary>
        [NativeDisableParallelForRestriction]
        public NativeArray<float3> positions;

        /// <summary>
        /// 현재 bone world rotation 배열입니다.
        /// </summary>
        [NativeDisableParallelForRestriction]
        public NativeArray<quaternion> rotations;

        /// <summary>
        /// 현재 bone local position 배열입니다.
        /// </summary>
        [NativeDisableParallelForRestriction]
        public NativeArray<float3> localPositions;

        /// <summary>
        /// 현재 bone local scale 배열입니다.
        /// </summary>
        [NativeDisableParallelForRestriction]
        public NativeArray<float3> localScales;

        /// <summary>
        /// 현재 bone local rotation 배열입니다.
        /// </summary>
        [NativeDisableParallelForRestriction]
        public NativeArray<quaternion> localRotations;

        /// <summary>
        /// 캐시된 bone 개수입니다.
        /// CustomAvatar.Length 또는 실제 Transform 배열 길이와 일치해야 합니다.
        /// </summary>
        public int bonesCounter;

        /// <summary>
        /// Persistent Allocator로 생성한 NativeArray들을 해제합니다.
        /// MotionMatching 컴포넌트 종료 시 반드시 호출해야 합니다.
        /// </summary>
        public void UnLoad()
        {
            positions.Dispose();
            rotations.Dispose();
            localScales.Dispose();
            localPositions.Dispose();
            localRotations.Dispose();
        }
    }
}
