using Unity.Collections;
using UnityEngine;

namespace Script.System.MotionSystemV2
{
    public abstract class PoseSearchChannelBase : ScriptableObject, IPoseSearchChannel
    {
        [SerializeField] private float weight = 1f;

        public float Weight { get => weight; set => weight = value; }

        public abstract int FeatureDimension { get; }
        public abstract string DebugName { get; }

        public abstract void BuildFeature(FeatureBuildContext ctx, NativeSlice<float> output);
        public abstract void BuildQuery(QueryBuildContext ctx, NativeSlice<float> output);
    }
}
