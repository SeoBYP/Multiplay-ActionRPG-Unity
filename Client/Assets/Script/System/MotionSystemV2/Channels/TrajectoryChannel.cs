using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Script.System.MotionSystemV2
{
    /// <summary>
    /// 캐릭터 이동 궤적을 Feature로 쓰는 채널.
    /// 샘플 포인트마다 position(x,z) + direction(x,z) = 4 float.
    /// </summary>
    [CreateAssetMenu(fileName = "TrajectoryChannel", menuName = "MotionMatchingV2/Channels/Trajectory")]
    public class TrajectoryChannel : PoseSearchChannelBase
    {
        [SerializeField] private TrajectorySample[] samples = Array.Empty<TrajectorySample>();

        public override int FeatureDimension => samples.Length * 4;
        public override string DebugName => "Trajectory";
        public TrajectorySample[] Samples => samples;

        /// <summary>
        /// 빌드 타임: 클립의 sampleTime 기준 궤적 샘플을 루트 로컬 스페이스로 추출.
        /// </summary>
        public override void BuildFeature(FeatureBuildContext ctx, NativeSlice<float> output)
        {
            int idx = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = math.clamp(ctx.sampleTime + samples[i].timeOffset, 0f, ctx.clipLength);

                float3 worldPos;
                float3 worldDir;

                if (ctx.sampleRootPosition != null)
                {
                    // PlayableGraph 기반 정확한 루트 위치 (Humanoid Root Motion 정상 동작)
                    worldPos = ctx.sampleRootPosition(t);
                    worldDir = SampleRootDirection(ctx, t);
                }
                else
                {
                    // Fallback: SampleAnimation 기반 (Generic rig 등)
                    worldPos = SampleRootPosition(ctx, t);
                    worldDir = SampleRootDirection(ctx, t);
                }

                float3 localPos = math.transform(ctx.rootInverse, worldPos);
                float3 localDir = math.rotate(ctx.rootInverse, worldDir);

                output[idx++] = localPos.x;
                output[idx++] = localPos.z;
                output[idx++] = localDir.x;
                output[idx++] = localDir.z;
            }
        }

        /// <summary>
        /// 런타임: 궤적 히스토리에서 각 timeOffset 보간값을 output에 씀.
        /// </summary>
        public override void BuildQuery(QueryBuildContext ctx, NativeSlice<float> output)
        {
            int idx = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                TrajectorySampleData s = InterpolateTrajectory(ctx.trajectoryHistory, samples[i].timeOffset);
                output[idx++] = s.position.x;
                output[idx++] = s.position.z;
                output[idx++] = s.direction.x;
                output[idx++] = s.direction.z;
            }
        }

        private static float3 SampleRootPosition(in FeatureBuildContext ctx, float time)
        {
            ctx.clip.SampleAnimation(ctx.rootTransform.gameObject, time);
            return ctx.rootTransform.position;
        }

        private static float3 SampleRootDirection(in FeatureBuildContext ctx, float time)
        {
            float nextTime = math.min(time + ctx.poseStep, ctx.clipLength);
            float3 p0 = SampleRootPosition(ctx, time);
            float3 p1 = SampleRootPosition(ctx, nextTime);
            float3 delta = new float3(p1.x - p0.x, 0f, p1.z - p0.z);
            float len = math.length(delta);
            if (len > 0.001f) return delta / len;
            float3 fwd = ctx.rootTransform.forward;
            return math.normalize(new float3(fwd.x, 0f, fwd.z));
        }

        private static TrajectorySampleData InterpolateTrajectory(
            NativeArray<TrajectorySampleData> history, float targetOffset)
        {
            if (!history.IsCreated || history.Length == 0)
                return new TrajectorySampleData { timeOffset = targetOffset };

            // targetOffset 이하인 마지막 인덱스를 찾는다
            int lo = 0;
            for (int i = 0; i < history.Length - 1; i++)
            {
                if (history[i + 1].timeOffset <= targetOffset) lo = i + 1;
                else break;
            }
            int hi = math.min(lo + 1, history.Length - 1);
            if (lo == hi) return history[lo];

            float t0 = history[lo].timeOffset;
            float t1 = history[hi].timeOffset;
            float range = t1 - t0;
            float t = range > 0.0001f ? (targetOffset - t0) / range : 0f;

            return new TrajectorySampleData
            {
                timeOffset = targetOffset,
                position  = math.lerp(history[lo].position, history[hi].position, t),
                direction = math.normalizesafe(
                    math.lerp(history[lo].direction, history[hi].direction, t),
                    history[lo].direction)
            };
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (samples == null) samples = Array.Empty<TrajectorySample>();
        }
#endif
    }
}
