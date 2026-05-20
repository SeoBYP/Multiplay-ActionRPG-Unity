using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Script.System.MotionSystemV2
{
    /// <summary>
    /// 캐릭터 본 위치/속도를 Feature로 쓰는 채널.
    /// 본마다 position(x,y,z) + velocity(x,y,z) = 6 float 고정.
    /// 로코모션에서는 LeftFoot / RightFoot 권장.
    /// </summary>
    [CreateAssetMenu(fileName = "PoseChannel", menuName = "MotionMatchingV2/Channels/Pose")]
    public class PoseChannel : PoseSearchChannelBase
    {
        [SerializeField] private SampledBone[] sampledBones = Array.Empty<SampledBone>();

        public override int FeatureDimension => sampledBones.Length * 6;
        public override string DebugName => "Pose";
        public SampledBone[] SampledBones => sampledBones;

        /// <summary>
        /// 빌드 타임: 클립의 sampleTime 기준 본 위치/속도를 루트 로컬 스페이스로 추출.
        /// </summary>
        public override void BuildFeature(FeatureBuildContext ctx, NativeSlice<float> output)
        {
            ctx.clip.SampleAnimation(ctx.rootTransform.gameObject, ctx.sampleTime);

            int idx = 0;
            for (int i = 0; i < sampledBones.Length; i++)
            {
                int boneIdx = (int)sampledBones[i].bone;
                bool valid = boneIdx >= 0
                             && ctx.boneTransforms != null
                             && boneIdx < ctx.boneTransforms.Length
                             && ctx.boneTransforms[boneIdx] != null;

                if (!valid)
                {
                    output[idx++] = 0f; output[idx++] = 0f; output[idx++] = 0f;
                    output[idx++] = 0f; output[idx++] = 0f; output[idx++] = 0f;
                    continue;
                }

                float3 worldPos = ctx.boneTransforms[boneIdx].position;
                float3 localPos = math.transform(ctx.rootInverse, worldPos);

                // 속도: 이전 프레임 로컬 위치와의 차분
                float3 prevLocal = ctx.prevBonePositions != null && boneIdx < ctx.prevBonePositions.Length
                    ? ctx.prevBonePositions[boneIdx]
                    : localPos;
                float3 velocity = ctx.poseStep > 0f ? (localPos - prevLocal) / ctx.poseStep : float3.zero;

                output[idx++] = localPos.x;
                output[idx++] = localPos.y;
                output[idx++] = localPos.z;
                output[idx++] = velocity.x;
                output[idx++] = velocity.y;
                output[idx++] = velocity.z;
            }
        }

        /// <summary>
        /// 런타임: NativeArray에서 본 위치/속도를 읽어 output에 씀.
        /// </summary>
        public override void BuildQuery(QueryBuildContext ctx, NativeSlice<float> output)
        {
            int idx = 0;
            for (int i = 0; i < sampledBones.Length; i++)
            {
                int boneIdx = (int)sampledBones[i].bone;
                bool validPos = ctx.bonePositions.IsCreated && boneIdx >= 0 && boneIdx < ctx.bonePositions.Length;
                bool validVel = ctx.boneVelocities.IsCreated && boneIdx >= 0 && boneIdx < ctx.boneVelocities.Length;

                float3 pos = validPos ? ctx.bonePositions[boneIdx] : float3.zero;
                float3 vel = validVel ? ctx.boneVelocities[boneIdx] : float3.zero;

                output[idx++] = pos.x;
                output[idx++] = pos.y;
                output[idx++] = pos.z;
                output[idx++] = vel.x;
                output[idx++] = vel.y;
                output[idx++] = vel.z;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (sampledBones == null) sampledBones = Array.Empty<SampledBone>();
        }
#endif
    }
}
