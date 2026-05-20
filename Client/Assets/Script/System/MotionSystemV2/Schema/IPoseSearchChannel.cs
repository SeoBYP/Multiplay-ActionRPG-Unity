using Unity.Collections;

namespace Script.System.MotionSystemV2
{
    public interface IPoseSearchChannel
    {
        int FeatureDimension { get; }
        float Weight { get; set; }
        string DebugName { get; }

        void BuildFeature(FeatureBuildContext ctx, NativeSlice<float> output);
        void BuildQuery(QueryBuildContext ctx, NativeSlice<float> output);
    }
}
