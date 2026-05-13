using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine.Jobs;

namespace Game.System.MotionSystem
{
  /// <summary>
  /// BlendingResults와 Inertialization offset을 실제 적용 가능한 bone/root 값으로 변환하는 기반 클래스입니다.
  /// root 적용 방식은 캐릭터 이동 시스템마다 다르므로 SetRootPose는 구현체에서 처리합니다.
  /// </summary>
  public abstract class PoseSetter
    {
        /// <summary>
        /// 보간된 bone pose에 inertialization offset decay를 더해 이번 frame에 Transform에 적용할 최종 값을 계산합니다.
        /// </summary>
        public static void GetNewBoneValues(
            ref NativeArray<OffsetBone> offsetsNative, 
            int index,
            BlendingResults blendingResults,
            NativeArray<float3> resultPositions,
            NativeArray<float3> resultScales,
            NativeArray<quaternion> resultRotations,
            NativeArray<quaternion> originalDiffRotations,
            quaternion rootRotation,
            int rootNode,
            float fixedDeltaTime,
            float halfLife,
            bool wantApplyPositions,
            bool wantApplyScales,
            bool wantApplyRootBonePosition)
        {
            if (math.isnan(blendingResults.bonesPosition[index].x)) return;
            
            var decayRotationResult = SpringUtils.DecaySpringRotation(offsetsNative[index].rotation, offsetsNative[index].angularVelocity, fixedDeltaTime, halfLife);

            var decayPositionResult = (float3.zero, float3.zero);
            var decayScaleResult = (float3.zero, float3.zero);
            if (index != rootNode ? wantApplyPositions : wantApplyRootBonePosition)
            {
                decayPositionResult = SpringUtils.DecaySpringPosition(offsetsNative[index].position, offsetsNative[index].velocity, fixedDeltaTime, halfLife);
                resultPositions[index] = blendingResults.bonesPosition[index] + decayPositionResult.Item1;
            }

            if (wantApplyScales)
            {
                decayScaleResult = SpringUtils.DecaySpringPosition(offsetsNative[index].scale, offsetsNative[index].scaleVelocity, fixedDeltaTime, halfLife);
                resultScales[index] = blendingResults.bonesScale[index] + decayScaleResult.Item1;
            }
            
            //Apply decayed offset
            var relativeRotation = math.mul(blendingResults.bonesRotation[index], decayRotationResult.Item1);
            resultRotations[index] = math.mul(math.mul(rootRotation, relativeRotation), originalDiffRotations[index]);
            
            offsetsNative[index] = new OffsetBone
            {
                rotation = decayRotationResult.Item1,
                angularVelocity = decayRotationResult.Item2,
                position = decayPositionResult.Item1,
                velocity = decayPositionResult.Item2,
                scale = decayScaleResult.Item1,
                scaleVelocity = decayScaleResult.Item2
            };
        }
        
        /// <summary>
        /// 보간된 root pose를 실제 이동 시스템에 적용합니다.
        /// </summary>
        public abstract void SetRootPose(BlendingResults blendingResults, CharacterControllerBase characterControllerBase);
    }

    /// <summary>
    /// 계산된 bone 결과 배열을 TransformAccessArray의 실제 bone Transform에 적용하는 job입니다.
    /// </summary>
    [BurstCompile]
    public struct SetBonesJob: IJobParallelForTransform
    {
        [ReadOnly, NativeDisableParallelForRestriction] public NativeArray<float3> finalPositionsNative;
        [ReadOnly, NativeDisableParallelForRestriction] public NativeArray<float3> finalScalesNative;
        [ReadOnly, NativeDisableParallelForRestriction] public NativeArray<quaternion> finalRotationsNative;
        [ReadOnly] public int rootNode;
        [ReadOnly] public bool wantApplyPositions;
        [ReadOnly] public bool wantApplyScales;
        [ReadOnly] public bool wantApplyRootBonePosition;
        [ReadOnly] public NativeArray<int> boneIndices;

        public void Execute(int index, TransformAccess transform)
        {
            if (!transform.isValid) return;
            int boneIndex = boneIndices[index];
            if (boneIndex != rootNode ? wantApplyPositions : wantApplyRootBonePosition)
            {
                transform.localPosition = finalPositionsNative[boneIndex];
            }

            if (wantApplyScales)
            {
                transform.localScale = finalScalesNative[boneIndex];
            }

            transform.rotation = finalRotationsNative[boneIndex];   //Now it is rotation as its relative
        }
    }
}
