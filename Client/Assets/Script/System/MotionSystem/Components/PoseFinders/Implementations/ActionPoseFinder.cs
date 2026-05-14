using Unity.Collections;
using Unity.Mathematics;

namespace Game.System.MotionSystem
{
    public class ActionPoseFinder : MotionPoseFinder
    {
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
            bool wantDebugDistance, 
            bool forceContinuousPose)
        {
            ActionQueryComputedFlow aqcf = (ActionQueryComputedFlow)queryComputedFlow;

            if (!aqcf.GetActionQueryComputed().actionTag.HasInitState() &&
                !aqcf.GetActionQueryComputed().actionTag.HasRecoveryState())
            {
                return FindSingleStateAction(
                    ref previousPositions,
                    ref timeOnLastPositions,
                    out currentMinDistance,
                    rtsModelInverse,
                    poseFinderGenericVariables,
                    currentBonesPosition,
                    bonesCount,
                    queryComputedFlow,
                    futureOffsets,
                    futureDirections,
                    pastTrajectory,
                    poseResult,
                    wantDebugDistance);
            }

            currentMinDistance = 0;
            var currentFeatures = aqcf.GetQueryComputed().featuresData[queryComputedFlow.currentFeatureID];
            aqcf.CurrentAnimationPoseID = currentFeatures.animFrame + 1;
            if (!aqcf.FirstFrame)
            {
                //aqcf.CurrentAnimationPoseID = 0;
                aqcf.FirstFrame = true;
                return queryComputedFlow.currentFeatureID;
            }
            
            /*if (aqcf.CurrentAnimationPoseID == -1)
            {
                aqcf.CurrentAnimationPoseID = 0;
                return aqcf.currentFeatureID;
            }*/
            
            var nextFrameID = aqcf.CurrentAnimationPoseID + 1;
            
            var animationPoses = aqcf.dataset.animationsData[currentFeatures.animationID];

            if (animationPoses.Count > nextFrameID)
            {
                aqcf.isSearch = false;
                return queryComputedFlow.currentFeatureID + 1;
            }
            
            //Go to Next State
            HandleNextState(aqcf);

            //Check init warping and collisions+physics
            aqcf.CheckInitWarpingPropertiesAndPhysicsSetup();
            
            UpdateAnimationIndexes(aqcf);
            return aqcf.currentFeatureID;
        }

        private int FindSingleStateAction(
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
            bool wantDebugDistances)
        {
            ActionQueryComputedFlow aqcf = (ActionQueryComputedFlow)queryComputedFlow;
            if (!aqcf.FirstFrame)
            {
                aqcf.FirstFrame = true;
                int selectedPose = GetNewPose(
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

                return TryGetActionEntrySkipRatio(aqcf, out float skipRatio)
                    ? SkipActionEntry(aqcf, selectedPose, skipRatio)
                    : selectedPose;
            }

            int nextFeatureID = queryComputedFlow.currentFeatureID + 1;
            if (queryComputedFlow.currentRange.featureIDStart <= nextFeatureID &&
                nextFeatureID <= queryComputedFlow.currentRange.featureIDStop)
            {
                currentMinDistance = 0f;
                aqcf.isSearch = false;
                return nextFeatureID;
            }

            currentMinDistance = 0f;
            aqcf.isQueryDone = true;
            aqcf.FirstFrame = false;
            return queryComputedFlow.currentFeatureID;
        }

        private static bool TryGetActionEntrySkipRatio(ActionQueryComputedFlow aqcf, out float skipRatio)
        {
            skipRatio = 0f;
            string[] query = aqcf.GetQueryComputed().query;
            if (query == null || query.Length == 0)
                return false;

            if (query[0].Contains("ToStop"))
            {
                skipRatio = 0.45f;
                return true;
            }

            if (query[0] == "WalkToRun")
            {
                skipRatio = 0.50f;
                return true;
            }

            return false;
        }

        private static int SkipActionEntry(ActionQueryComputedFlow aqcf, int selectedPose, float skipRatio)
        {
            // TODO(MotionMatching Editor): move this temporary stop-entry rule to editable
            // MotionSearchDatabase/action settings. Transition clips should expose per-query
            // entry mode, normalized time, and frame clamp instead of a hard-coded ratio.
            QueryRange range = aqcf.currentRange;
            if (range.featureIDStop <= range.featureIDStart)
                return selectedPose;

            int skippedFrames = math.max(2, (int)math.round((range.featureIDStop - range.featureIDStart) * skipRatio));
            int firstAllowedPose = range.featureIDStart + skippedFrames;
            return math.clamp(math.max(selectedPose, firstAllowedPose), range.featureIDStart, range.featureIDStop);
        }
        
        public void HandleNextState(ActionQueryComputedFlow aqcf)
        {
            aqcf.CurrentState = aqcf.CurrentState switch
            {
                ActionTagState.Init => ActionTagState.InProgress,
                ActionTagState.InProgress => ActionTagState.Recovery,
                _ => ActionTagState.Init
            };

            //Check whether this Action has init and recovery states
            ActionTag actionTag = aqcf.GetActionQueryComputed().actionTag;
            if (!actionTag.HasRecoveryState() && aqcf.CurrentState == ActionTagState.Recovery)
                aqcf.CurrentState = ActionTagState.Init;

            if (aqcf.CurrentState == ActionTagState.Init)
            {
                //Set isDone = true
                aqcf.isQueryDone = true;
                if (!actionTag.HasInitState())
                {
                    aqcf.CurrentState = ActionTagState.InProgress;
                }

                return;
            }
            aqcf.isSearch = true;
        }
        
        public void UpdateAnimationIndexes(ActionQueryComputedFlow aqcf)
        {
            if (aqcf.isQueryDone)
            {
                aqcf.CurrentAnimationPoseID = -1;
                return;
            }
            int state = (int)aqcf.CurrentState;
            aqcf.CurrentAnimationPoseID = -1;//aqcf.GetActionQueryComputed().actionTag.ranges[state].frameStart;
            aqcf.currentFeatureID = aqcf.GetRanges()[state].featureIDStart; //aqcf.GetActionQueryComputed().actionTag.ranges[state].poseStart;
        }
        
        public void UpdateAnimationIndexesByTime(ActionQueryComputedFlow aqcf, float time)
        {
            int state = (int)aqcf.CurrentState;
            
            //Get current feature by state time
            int currentFeature =
                (int)((aqcf.GetRanges()[state].featureIDStop - aqcf.GetRanges()[state].featureIDStart) * time +
                aqcf.GetRanges()[state].featureIDStart);
            
            //Apply it to pose and feature
            aqcf.currentFeatureID = currentFeature;
            aqcf.CurrentAnimationPoseID = aqcf.GetQueryComputed().featuresData[aqcf.currentFeatureID].animFrame; 
        }
    }
}
