using Unity.Burst;
using Unity.Collections;
using UnityEngine.Jobs;

namespace Game.System.MotionSystem
{
    /// <summary>
    /// Runtime character의 현재 bone Transform 값을 CurrentBoneTransformsValues에 복사하는 Transform job입니다.
    /// Pose search와 blending은 현재 pose를 기준으로 candidate pose와의 차이를 계산해야 하므로,
    /// 매 frame 또는 검색 직전에 이 job으로 skeleton cache를 갱신합니다.
    /// </summary>
    [BurstCompile]
    public struct GetCurrentTransformValues : IJobParallelForTransform
    {
        /// <summary>
        /// job이 기록할 현재 bone transform cache입니다.
        /// TransformAccessArray의 index와 이 NativeArray들의 index는 같은 bone을 가리켜야 합니다.
        /// </summary>
        public CurrentBoneTransformsValues boneTransformsValues;
        [ReadOnly] public NativeArray<int> boneIndices;

        /// <summary>
        /// TransformAccessArray의 각 bone Transform을 읽어 world/local position, rotation, scale을 캐시합니다.
        /// </summary>
        public void Execute(int index, TransformAccess transform)
        {
            int boneIndex = boneIndices[index];
            boneTransformsValues.positions[boneIndex] = transform.position;
            boneTransformsValues.rotations[boneIndex] = transform.rotation;
            boneTransformsValues.localPositions[boneIndex] = transform.localPosition;
            boneTransformsValues.localRotations[boneIndex] = transform.localRotation;
            boneTransformsValues.localScales[boneIndex] = transform.localScale;
        }
    }
}
