using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Game.System.MotionSystem
{
    /// <summary>
    /// 일반 이동 query용 PoseFinder입니다.
    /// 일정 searchRate마다 전체 후보를 검색하고, 그 사이 frame에서는 현재 range 안의 다음 pose를 연속 재생합니다.
    /// </summary>
    public class MotionPoseFinder : PoseFinder
    {
        private const int FramesToAvoid = 3;

        /// <summary>
        /// searchRate, continuous pose 가능 여부, 강제 연속 재생 여부를 기준으로 새 검색 또는 다음 frame 진행을 선택합니다.
        /// </summary>
        public override int Find(
            ref NativeArray<float3> previousPositions,
            ref float timeOnLastPositions,
            out float currentMinDistance,
            float4x4 rtsModelInverse,
            PoseFinderGenericVariables poseFinderGenericVariables,
            NativeArray<float3> currentBonesPosition,
            int bonesCount,
            QueryComputedFlow queryComputedFlow,
            NativeArray<float3> futureOffsets, 
            NativeArray<float3> futureDirections,
            PastTrajectory pastTrajectory,
            NativeArray<DistanceResult> poseResult,
            bool wantDebugDistances,
            bool forceContinuousPose){

            if (Counter >= queryComputedFlow.searchRate && !forceContinuousPose)
            {
                Counter = 0;
                return GetNewPose(
                    ref previousPositions,  
                    out currentMinDistance, 
                    rtsModelInverse,
                    poseFinderGenericVariables,
                    currentBonesPosition,
                    bonesCount,
                    timeOnLastPositions,
                    queryComputedFlow,
                    futureOffsets, 
                    futureDirections,
                    pastTrajectory,
                    poseResult,
                    wantDebugDistances,
                    false);
            }
            
            var continuousPose = TryGetContinuousPose(queryComputedFlow.currentFeatureID, queryComputedFlow.currentRange, forceContinuousPose);
            if (continuousPose != -1)
            {
                Counter += queryComputedFlow.dataset.poseStep;
                queryComputedFlow.isSearch = false;
                currentMinDistance = 0;
                return continuousPose;
            }
            
            Counter = 0;
            return GetNewPose(
                ref previousPositions,  
                out currentMinDistance, 
                rtsModelInverse,
                poseFinderGenericVariables,
                currentBonesPosition,
                bonesCount,
                timeOnLastPositions, 
                queryComputedFlow,
                futureOffsets, 
                futureDirections, 
                pastTrajectory,
                poseResult,
                wantDebugDistances,
                true);
        }

        /// <summary>
        /// 현재 query range 안에서 다음 feature를 그대로 이어갈 수 있는지 확인합니다.
        /// </summary>
        private int TryGetContinuousPose(int currentFeatureID, QueryRange currentRange, bool forceContinuousPose)
        {
            var nextFeatureID = currentFeatureID + 1;
            try
            {
                if (currentRange.featureIDStart <= nextFeatureID && nextFeatureID <= currentRange.featureIDStop)
                {
                    return nextFeatureID;
                }

                if (forceContinuousPose)
                {
                    return currentRange.featureIDStart;
                }
                return -1;
            }
            catch
            {
                Debug.LogError("**************************");
                return -1;
            }
        }

        /// <summary>
        /// future/past/current pose feature를 만들고, range별 최소 distance를 계산해 best pose feature를 반환합니다.
        /// </summary>
        protected virtual int GetNewPose(
            ref NativeArray<float3> previousPositions,
            out float currentMinDistance,
            float4x4 rtsModelInverse,
            PoseFinderGenericVariables poseFinderGenericVariables,
            NativeArray<float3> currentBonesPosition,
            int bonesCount,
            float timeOnLastPositions, 
            QueryComputedFlow queryComputedFlow, 
            NativeArray<float3> futureOffsets, 
            NativeArray<float3> futureDirections,
            PastTrajectory pastTrajectory,
            NativeArray<DistanceResult> poseResult,
            bool wantDebugDistances,
            bool isLastFrame)
        {
            queryComputedFlow.isSearch = true;

            var queryComputedNative = queryComputedFlow.GetQueryComputed().GetFeaturesQueryComputedNative();
            new NormalizeFuturesAndPastsJob
            {
                rtsModelInverse = rtsModelInverse,
                futureDirections = futureDirections,
                futureOffsets = futureOffsets,
                meanFutureDirection = queryComputedNative.meanFutureDirections,
                meanFutureOffset = queryComputedNative.meanFutureOffsets,
                stdFutureDirection = queryComputedNative.stdFutureDirections,
                stdFutureOffset = queryComputedNative.stdFutureOffsets,
                normalizedFutureDirections = poseFinderGenericVariables.normalizedFutureDirections,
                normalizedFutureOffsets = poseFinderGenericVariables.normalizedFutureOffsets,
                pastGlobalDirections = pastTrajectory.pastGlobalDirection,
                pastGlobalPositions = pastTrajectory.pastGlobalPosition,
                meanPastDirection = queryComputedNative.meanPastDirections,
                meanPastOffset = queryComputedNative.meanPastOffsets,
                stdPastDirection = queryComputedNative.stdPastDirections,
                stdPastOffset = queryComputedNative.stdPastOffsets,
                normalizedPastsOffsets = poseFinderGenericVariables.normalizedPastOffsets,
                normalizedPastsDirections = poseFinderGenericVariables.normalizedPastDirections
            }.Schedule().Complete();

            CreateCurrentFeatures(
                ref previousPositions,
                ref poseFinderGenericVariables.currentFeatures,
                rtsModelInverse,
                currentBonesPosition,
                bonesCount,
                poseFinderGenericVariables.currentPositions,
                timeOnLastPositions,
                queryComputedNative.meanFeatureVelocity,
                queryComputedNative.stdFeatureVelocity,
                queryComputedNative.meanFeaturePosition,
                queryComputedNative.stdFeaturePosition
            );

            FeatureDataNative currentFeature = new FeatureDataNative
            {
                futureDirections = poseFinderGenericVariables.normalizedFutureDirections,
                futureOffsets = poseFinderGenericVariables.normalizedFutureOffsets,
                pastDirections = poseFinderGenericVariables.normalizedPastDirections,
                pastOffsets = poseFinderGenericVariables.normalizedPastOffsets,
                positionsAndVelocities = poseFinderGenericVariables.currentFeatures
            };
            var ranges = queryComputedFlow.GetRangesNative();
            var batchCount = Math.Max(1, JobsUtility.JobWorkerCount / ranges.Length);
            new GetMinimumDistanceJob
            {
                ranges = ranges,
                currentFeature = currentFeature,
                bonesWeights = queryComputedFlow.bonesWeights,
                globalWeights = queryComputedFlow.globalWeights,
                featuresDataAnims = queryComputedNative.featureDataAnims,
                featuresDataPositionsAndVelocities = queryComputedNative.featuresPositionsAndVelocities,
                featuresDataFutureOffsets = queryComputedNative.featuresFutureOffsets,
                featuresDataFutureDirections = queryComputedNative.featuresFutureDirections,
                featuresDataPastOffsets = queryComputedNative.featuresPastOffsets,
                featuresDataPastDirections = queryComputedNative.featuresPastDirections,
                currentFeatureID = queryComputedFlow.currentFeatureID,
                currentAnimationID = queryComputedNative.featureDataAnims[queryComputedFlow.currentFeatureID].animationID,
                animationSwitchPenalty = queryComputedFlow.animationSwitchPenalty,
                isLastFrame = isLastFrame,
                wantDebugDistances = wantDebugDistances,
                distanceResults = queryComputedFlow.distanceResults
            }.Schedule(ranges.Length, batchCount).Complete();
            
            new ProcessDistanceResultsJob
            {
                distanceResults = queryComputedFlow.distanceResults,
                result = poseResult
            }.Schedule().Complete();

            currentMinDistance = poseResult[0].distance;
            queryComputedFlow.currentRange = poseResult[0].queryRange;
            var result = poseResult[0].pose;

            return result;
        }
        
        [BurstCompile]
        private struct GetMinimumDistanceJob: IJobParallelFor
        {
            [ReadOnly] public int currentFeatureID;
            [ReadOnly] public NativeArray<QueryRange> ranges;
            [ReadOnly] public FeatureDataNative currentFeature;
            [ReadOnly] public BonesWeights bonesWeights;
            [ReadOnly] public GlobalWeights globalWeights;
            [ReadOnly] public NativeArray<FeatureDataAnim> featuresDataAnims;
            [ReadOnly] public NativeArray<float3> featuresDataPositionsAndVelocities;
            [ReadOnly] public NativeArray<float3> featuresDataFutureOffsets;
            [ReadOnly] public NativeArray<float3> featuresDataFutureDirections;
            [ReadOnly] public NativeArray<float3> featuresDataPastOffsets;
            [ReadOnly] public NativeArray<float3> featuresDataPastDirections;
            [ReadOnly] public int currentAnimationID;
            [ReadOnly] public float animationSwitchPenalty;
            [ReadOnly] public bool wantDebugDistances;
            [ReadOnly] public bool isLastFrame;
            
            [NativeDisableParallelForRestriction]
            public NativeArray<DistanceResult> distanceResults;
            public void Execute(int rangeIndex)
            {
                float minDistance = -1;
                int finalPose = 0;
                for (var index = ranges[rangeIndex].featureIDStart; index < ranges[rangeIndex].featureIDStop; index++)
                {
                    if (index >= currentFeatureID - FramesToAvoid && index <= currentFeatureID ||
                        ranges[rangeIndex].featureIDStop < index + 1)
                    {
                        continue;
                    }

                    var distance = GetTotalDistance(
                        index,
                        currentFeature,
                        bonesWeights,
                        globalWeights,
                        featuresDataAnims,
                        featuresDataPositionsAndVelocities,
                        featuresDataFutureOffsets,
                        featuresDataFutureDirections,
                        featuresDataPastOffsets,
                        featuresDataPastDirections,
                        wantDebugDistances,
                        isLastFrame);
                    var distanceVal = distance;
                    if (featuresDataAnims[index].animationID != currentAnimationID)
                    {
                        distanceVal += animationSwitchPenalty;
                    }

                    if (math.isnan(distanceVal) || math.isinf(distanceVal))
                    {
                        continue;
                    }

                    if (minDistance >= 0
                        && distanceVal >= minDistance)
                    {
                        continue;
                    }

                    minDistance = distanceVal;
                    
                    finalPose = index;
                }
                var finalDistance = distanceResults[rangeIndex];
                finalDistance.distance = minDistance;
                finalDistance.pose = finalPose;
                finalDistance.queryRange = ranges[rangeIndex];
                distanceResults[rangeIndex] = finalDistance;
            }
        }

        [BurstCompile]
        private struct NormalizeFuturesAndPastsJob: IJob
        {
            [ReadOnly] public NativeArray<float3> futureDirections;
            [ReadOnly] public NativeArray<float3> futureOffsets;
            [ReadOnly] public NativeArray<float3> meanFutureDirection;
            [ReadOnly] public NativeArray<float3> stdFutureDirection;
            [ReadOnly] public NativeArray<float3> meanFutureOffset;
            [ReadOnly] public NativeArray<float3> stdFutureOffset;
            
            [ReadOnly] public NativeArray<float3> pastGlobalDirections;
            [ReadOnly] public NativeArray<float3> pastGlobalPositions;
            [ReadOnly] public NativeArray<float3> meanPastDirection;
            [ReadOnly] public NativeArray<float3> stdPastDirection;
            [ReadOnly] public NativeArray<float3> meanPastOffset;
            [ReadOnly] public NativeArray<float3> stdPastOffset;

            [ReadOnly] public float4x4 rtsModelInverse;
            
            public NativeArray<float3> normalizedFutureDirections;
            public NativeArray<float3> normalizedFutureOffsets;
            public NativeArray<float3> normalizedPastsDirections;
            public NativeArray<float3> normalizedPastsOffsets;
            
            public void Execute()
            {
                for (int i = 0; i < normalizedFutureDirections.Length; i++)
                {
                    normalizedFutureDirections[i] =
                        (futureDirections[i] - meanFutureDirection[i]) / stdFutureDirection[i];
                    normalizedFutureDirections[i] = normalizedFutureDirections[i].Sanitize();
                    normalizedFutureOffsets[i] =
                        (futureOffsets[i] - meanFutureOffset[i]) / stdFutureOffset[i];
                    normalizedFutureOffsets[i] = normalizedFutureOffsets[i].Sanitize();
                }
                
                for (int i = 0; i < normalizedPastsDirections.Length; i++)
                {
                    var pastOffsetPosition = MathUtils.TranslateToLocal(rtsModelInverse, pastGlobalPositions[i]);
                    var pastOffsetDirection= math.normalize(
                        math.mul(rtsModelInverse, new float4(pastGlobalDirections[i], 0.0f)).xyz);

                    normalizedPastsDirections[i] =
                        (pastOffsetDirection - meanPastDirection[i]) / stdPastDirection[i];
                    normalizedPastsDirections[i] = normalizedPastsDirections[i].Sanitize();
                    normalizedPastsOffsets[i] =
                        (pastOffsetPosition - meanPastOffset[i]) / stdPastOffset[i];
                    normalizedPastsOffsets[i] = normalizedPastsOffsets[i].Sanitize();
                }
            }
        }

        [BurstCompile]
        private struct ProcessDistanceResultsJob: IJob
        {
            public NativeArray<DistanceResult> distanceResults;
            public NativeArray<DistanceResult> result;

            public void Execute()
            {
                float minDistance = -1;
                int finalPose = 0;
                QueryRange finalRange = default;
                int finalIndex = 0;
                for (var i = 0; i < distanceResults.Length; i++)
                {
                    if (distanceResults[i].distance == 0)
                    {
                        continue;
                    }

                    var currentDistance = distanceResults[i].distance;
                    if (currentDistance < 0 || math.isnan(currentDistance) || math.isinf(currentDistance))
                    {
                        continue;
                    }
                    
                    if (minDistance >= 0 
                        && currentDistance >= minDistance)
                    {
                        continue;
                    }

                    finalIndex = i;
                    finalRange = distanceResults[i].queryRange;
                    finalPose = distanceResults[i].pose;
                    minDistance = currentDistance;
                }

                result[0] = new DistanceResult()
                {
                    index = finalIndex,
                    queryRange = finalRange,
                    distance = minDistance,
                    pose = finalPose
                };
            }
        }
        
        private static float GetTotalDistance(
            int index,
            FeatureDataNative currentFeature, 
            BonesWeights bonesWeights,
            GlobalWeights globalWeights,
            NativeArray<FeatureDataAnim> featureDataAnims,
            NativeArray<float3> positionsAndVelocities,
            NativeArray<float3> futureOffsets,
            NativeArray<float3> futureDirections,
            NativeArray<float3> pastOffsets,
            NativeArray<float3> pastDirections,
            bool wantDebugDistances,
            bool isLastFrame = false)
        {
            float totalBonePosDistance = 0f;
            float totalBoneVelDistance = 0f;
            
            var offsetPositionAndVelocities = index * currentFeature.positionsAndVelocities.Length;
            var offsetFutureOffsets = index * currentFeature.futureOffsets.Length;
            var offsetFutureDirections = index * currentFeature.futureDirections.Length;
            var offsetPastOffsets = index * currentFeature.pastOffsets.Length;
            var offsetPastDirections = index * currentFeature.pastDirections.Length;
            var featureDataAnim = featureDataAnims[index];
            for (var i = 0; i < currentFeature.positionsAndVelocities.Length; i++) //Dont transform to LINQ
            {
                if (bonesWeights.weights[i] == 0)
                {
                    continue;
                }

                var featurePosOrVelocity = positionsAndVelocities[offsetPositionAndVelocities + i];
                if (math.isnan(featurePosOrVelocity.x))
                {
                    continue;
                }

                if (i % 2 == 0)
                {
                    totalBonePosDistance += (GetDistance(featurePosOrVelocity, currentFeature.positionsAndVelocities[i]))
                                            * bonesWeights.weights[i];
                    continue;
                }

                if (featureDataAnim.animFrame == 0 && isLastFrame)
                {
                    continue;
                }
                totalBoneVelDistance += (GetDistance(featurePosOrVelocity, currentFeature.positionsAndVelocities[i]))
                                        * bonesWeights.weights[i];
            }
            
            totalBonePosDistance = SafeDivide(totalBonePosDistance, bonesWeights.totalWeightPositions);
            totalBoneVelDistance = SafeDivide(totalBoneVelDistance, bonesWeights.totalWeightVelocities);
            
            var totalBoneDistance = totalBonePosDistance * globalWeights.weightBonesPosition + totalBoneVelDistance * globalWeights.weightBonesVelocity;

            var futPosDist = GetFutureOffsetDistance(offsetFutureOffsets, futureOffsets, currentFeature);
            var futPosDistW = futPosDist * bonesWeights.weightFutureOffset;
            
            var futDirDist = GetFutureDirectionDistance(offsetFutureDirections, futureDirections, currentFeature);
            var futDirDistW = futDirDist * bonesWeights.weightFutureDirection;
            
            var pastPosDist = GetPastOffsetDistance(offsetPastOffsets, pastOffsets, currentFeature);
            var pastPosDistW = pastPosDist * bonesWeights.weightPastOffset;
            
            var pastDirDist = GetPastDirectionDistance(offsetPastDirections, pastDirections, currentFeature);
            var pastDirDistW = pastDirDist * bonesWeights.weightPastDirection;

            var rootFutureDistances = (futPosDistW * globalWeights.weightFutureRootPosition 
                                       + futDirDistW * globalWeights.weightFutureRootDirection) * globalWeights.weightFutures;
            
            var rootPatsDistances = (pastPosDistW * globalWeights.weightPastRootPosition
                                     + pastDirDistW * globalWeights.weightPastRootDirection) * globalWeights.weightpasts;
            
            var totalBoneDistanceW = totalBoneDistance * globalWeights.weightBones;
            return SanitizeDistance(rootFutureDistances + rootPatsDistances + totalBoneDistanceW);
        }

        private static float GetFutureOffsetDistance(int offset, NativeArray<float3> futureOffsets, FeatureDataNative currentFeature)
        {
            float totalDistance = 0f;
            for (var i = 0; i < currentFeature.futureOffsets.Length; i++) // Dont transform to LINQ
            {
                totalDistance += (GetDistance(futureOffsets[offset + i], currentFeature.futureOffsets[i])/* / _dataset.maxFutureOffsetsDistance[i]*/);
            }
            return totalDistance;
        }
        
        private static float GetFutureDirectionDistance(int offset, NativeArray<float3> futureDirections, FeatureDataNative currentFeature)
        {
            float totalDistance = 0f;
            for (var i = 0; i < currentFeature.futureDirections.Length; i++) // Dont transform to LINQ
            {
                totalDistance += (GetDistance(futureDirections[offset + i], currentFeature.futureDirections[i])/* / _dataset.maxFutureDirectionsDistance[i]*/);
            }
            return totalDistance;
        }
        
        private static float GetPastOffsetDistance(int offset, NativeArray<float3> pastOffsets, FeatureDataNative currentFeature)
        {
            float totalDistance = 0f;
            for (var i = 0; i < currentFeature.pastOffsets.Length; i++) // Dont transform to LINQ
            {
                totalDistance += (GetDistance(pastOffsets[offset + i], currentFeature.pastOffsets[i])/* / _dataset.maxFutureOffsetsDistance[i]*/);
            }
            return totalDistance;
        }
        
        private static float GetPastDirectionDistance(int offset, NativeArray<float3> pastDirections, FeatureDataNative currentFeature)
        {
            float totalDistance = 0f;
            for (var i = 0; i < currentFeature.pastDirections.Length; i++) // Dont transform to LINQ
            {
                totalDistance += (GetDistance(pastDirections[offset + i], currentFeature.pastDirections[i])/* / _dataset.maxFutureDirectionsDistance[i]*/);
            }
            return totalDistance;
        }
        
        private static float GetDistance(float3 feature, float3 current)
        {
            var distance = math.distance(current.Sanitize(), feature.Sanitize());
            return SanitizeDistance(distance);
        }
        
        private static float GetDirectionDistance(float3 feature, float3 current)
        {
            return SanitizeDistance(1 - math.dot(current.Sanitize(), feature.Sanitize()));
        }

        private static float SafeDivide(float value, float divisor)
        {
            if (math.abs(divisor) < 0.00001f || math.isnan(divisor) || math.isinf(divisor))
                return 0f;

            return SanitizeDistance(value / divisor);
        }

        private static float SanitizeDistance(float value)
        {
            return math.isnan(value) || math.isinf(value) ? 0f : value;
        }
    }
}
