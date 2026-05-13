using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Game.System.MotionSystem
{
    /// <summary>
    /// 새 pose로 전환할 때 현재 pose와 target pose 사이의 차이를 offset으로 보존하고,
    /// spring decay로 서서히 줄여 snap을 완화하는 유틸리티입니다.
    /// </summary>
    public static class Inertialization
    {
        /// <summary>
        /// 현재 runtime transform 값과 직전 motion data를 비교해 bone별 velocity/angular velocity를 갱신합니다.
        /// Pose search 직전에 호출되어 inertialization과 current feature 계산의 기준 데이터를 최신화합니다.
        /// </summary>
        public static void UpdateMotionData(
            NativeArray<BoneData> nextBoneDatas,
            AnimationData nextAnimationData, 
            NativeArray<float3> localPositions,
            NativeArray<float3> localScales,
            NativeArray<quaternion> worldRotations,
            NativeArray<quaternion> originalDiffs,
            quaternion rootRotation,
            int bonesCount,
            float timeSinceLastUpdate,
            float poseStep,
            ref MotionData motionData)
        {
            nextBoneDatas.CopyFrom(nextAnimationData.bonesData);

            UpdateMotionDataJob updateMotionDataJob = new UpdateMotionDataJob()
            {
                nextBoneDatas = nextBoneDatas,
                motionData = motionData,
                timeSinceLastUpdate = timeSinceLastUpdate,
                localPositions = localPositions,
                localScales = localScales,
                worldRotations = worldRotations,
                rootRotation = rootRotation,
                originalDiffs = originalDiffs
            };

            var batchCount = Mathf.Max(1, JobsUtility.JobWorkerCount / bonesCount);
            JobHandle updateMotionDataJobHandle = updateMotionDataJob.Schedule(bonesCount, batchCount);
            updateMotionDataJobHandle.Complete();
        }
        
        [BurstCompile]
        public struct UpdateMotionDataJob: IJobParallelFor
        {
            [ReadOnly]
            public NativeArray<BoneData> nextBoneDatas;
            [ReadOnly]
            public NativeArray<float3> localPositions;
            [ReadOnly]
            public NativeArray<float3> localScales;
            [ReadOnly]
            public NativeArray<quaternion> worldRotations;
            [ReadOnly]
            public NativeArray<quaternion> originalDiffs;
            [ReadOnly]
            public quaternion rootRotation;
            [ReadOnly]
            public float timeSinceLastUpdate;

            public MotionData motionData;

            public void Execute(int index)
            {
                BoneData currentTransformData = nextBoneDatas[index];
                if (!currentTransformData.isValid) return;                                                              
                
                var currentRelativeRotation = math.mul(math.inverse(rootRotation), worldRotations[index]);  //We get the root-relative rotation
                currentRelativeRotation = math.mul(currentRelativeRotation, math.inverse(originalDiffs[index]));    //The we transform the current character space
                                                                                                                        //to the recorded root space

                motionData.AngularVelocities[index] = MathUtils.AngularVelocity(
                    motionData.LastRotations[index],
                    currentRelativeRotation, 
                    timeSinceLastUpdate).Sanitize();

                motionData.LastRotations[index] = currentRelativeRotation;
                motionData.LastVelocities[index] = ((localPositions[index] - motionData.LastPositions[index]) / timeSinceLastUpdate).Sanitize();
                motionData.LastPositions[index] = localPositions[index];
                motionData.LastVelocityScales[index] = ((localScales[index] - motionData.LastScales[index]) / timeSinceLastUpdate).Sanitize();
                motionData.LastScales[index] = localScales[index];
            }
        }

        /// <summary>
        /// 선택된 target pose로 전환하기 위한 bone별 rotation/position/scale offset을 초기화합니다.
        /// </summary>
        public static void InitBoneTransition(
            ref MotionData motionData, 
            int index, 
            NativeArray<BoneData> boneDatas, 
            int rootNode,
            bool wantApplyPositions,
            bool wantApplyScales)
        {
            var offsetBone = motionData.Offsets[index];
            NewTransition(ref offsetBone, motionData.LastRotations[index], motionData.AngularVelocities[index], boneDatas[index]);
                
            if (wantApplyPositions)
            {
                NewTransitionLocalPosition(ref offsetBone, motionData.LastPositions[index], motionData.LastVelocities[index], boneDatas[index]);
            }
            
            if (wantApplyScales)
            {
                NewTransitionScale(ref offsetBone, motionData.LastScales[index], motionData.LastVelocityScales[index], boneDatas[index]);    
            }
                
            motionData.Offsets[index] = offsetBone;
        }
        
        /// Inits a new transition offset on a selected bone
        private static void NewTransition(ref OffsetBone offset, quaternion currentRotation, float3 currentAngular,
            BoneData targetAnimationData)
        {
            //Offset -> variable to store offsets from current bone
            //currentRot, the current rotation of the current bone
            //targetAnimData, the target animation pose

            //Rotation offset
            quaternion targetRot = targetAnimationData.rotation;
            quaternion rotOffset = math.mul(math.inverse(targetRot), currentRotation);
            rotOffset = math.normalizesafe(MathUtils.Abs(rotOffset));

            offset.rotation = rotOffset;

            //Angular velocity offset
            offset.angularVelocity = currentAngular - targetAnimationData.angularVelocity;
        }
        
        private static void NewTransitionLocalPosition(ref OffsetBone offset, float3 currentPos, float3 currentVel,
            BoneData targetAnimationData)
        {
            offset.position = currentPos - targetAnimationData.localPosition;
            offset.velocity = currentVel - targetAnimationData.localVelocity;
        }
        
        private static void NewTransitionScale(ref OffsetBone offset, float3 currentScale, float3 currentScaleVel, BoneData targetAnimationData)
        {
            //Debug.Log("CurrentScale -> " + currentScale + " ; targetAnimationData.scale -> " + targetAnimationData.scale + " ; offset -> " + (currentScale - targetAnimationData.scale));
            offset.scale = currentScale - targetAnimationData.scale;
            offset.scaleVelocity = currentScaleVel;
        }

        /// <summary>
        /// 런타임 시작 시 현재 skeleton pose를 MotionData의 기준값으로 채웁니다.
        /// </summary>
        public static void FirstFillInMotionData(ref MotionData motionData, NativeArray<quaternion> originalDiffs, 
            quaternion rootRotation, Transform[] characterTransforms, int bonesLenght)
        {
            //Init rotation offsets as identity, angular as zero and last rotations as current
            for (int i = 0; i < bonesLenght; i++)
            {
                var currentOffset = motionData.Offsets[i];
                currentOffset.rotation = quaternion.identity;
                currentOffset.angularVelocity = float3.zero;
                currentOffset.velocity = float3.zero;
                motionData.Offsets[i] = currentOffset;

                var characterTransform = characterTransforms[i];

                if (characterTransform == null)
                {
                    motionData.LastRotations[i] = quaternion.identity;
                    motionData.LastPositions[i] = float3.zero;
                    motionData.LastScales[i] = float3.zero;
                }
                else
                {
                    var currentRelativeRotation = math.mul(
                        math.inverse(rootRotation),              
                        characterTransform.rotation);
                    currentRelativeRotation = math.mul(currentRelativeRotation, math.inverse(originalDiffs[i]));
                    
                    motionData.LastRotations[i] = currentRelativeRotation;
                    motionData.LastPositions[i] = characterTransform.localPosition;
                    motionData.LastScales[i]    = characterTransform.localScale;
                }
            }
        }
        
        /// <summary>
        /// 특정 baked pose를 기준으로 MotionData를 직접 초기화합니다.
        /// 액션을 특정 normalized time에서 시작하는 경우처럼 즉시 pose 기준을 맞출 때 사용합니다.
        /// </summary>
        public static void FillInMotionDataByPose(ref MotionData motionData, NativeArray<quaternion> originalDiffs, 
            Transform[] characterTransforms, int bonesLenght, BoneData[] bonesData)
        {
            //Init rotation offsets as identity, angular as zero and last rotations as current
            for (int i = 0; i < bonesLenght; i++)
            {
                var currentOffset = motionData.Offsets[i];
                currentOffset.rotation = quaternion.identity;
                currentOffset.angularVelocity = float3.zero;
                currentOffset.velocity = float3.zero;
                motionData.Offsets[i] = currentOffset;

                var boneData = bonesData[i];
                
                if (characterTransforms[i] == null)
                {
                    motionData.LastRotations[i] = quaternion.identity;
                    motionData.LastPositions[i] = float3.zero;
                    motionData.LastScales[i] = float3.zero;
                }
                else
                {
                    var currentRelativeRotation = 
                        math.mul(boneData.rotation, math.inverse(originalDiffs[i]));
                    
                    //motionData.LastRotations[i] = currentRelativeRotation;
                    motionData.LastPositions[i] = boneData.localPosition;
                    motionData.LastScales[i]    = boneData.scale;
                }
            }
        }
    }

}
