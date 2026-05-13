using UnityEngine;
using Unity.Collections;
using UnityEngine.Jobs;

namespace Game.System.MotionSystem
{
    public class MotionQueryComputedFlow : QueryComputedFlow
    {
        public MotionQueryComputedFlow(
            Dataset dataset, 
            Transform root, 
            CurrentBoneTransformsValues currentBoneTransformsValues,
            TransformAccessArray characterTransformsNative,
            NativeArray<int> characterTransformBoneIndices,
            GlobalWeights globalWeights, 
            float searchRate,
            float animationSwitchPenalty) 
            : base(dataset, root, currentBoneTransformsValues, characterTransformsNative, characterTransformBoneIndices, globalWeights, searchRate, animationSwitchPenalty)
        {
            PoseFinder = new MotionPoseFinder();
            PoseSetter = new MotionPoseSetter();
        }

        public override void Build(QueryComputed queryComputed, int length)
        {
            SetQueryComputed(queryComputed);
            ManageWeights(length);
            InitializeDistanceResults();
        }
    }
}
