using Unity.Collections;

namespace Script.System.MotionSystemV2
{
    /// <summary>
    /// Database의 평탄한 float NativeArray에서 포즈 한 장 분량의 Feature를 참조하는 뷰.
    /// 복사 없이 NativeSlice로 데이터를 참조하므로 할당이 없다.
    /// </summary>
    public readonly struct FeatureVectorView
    {
        public readonly NativeSlice<float> Features;
        public readonly PoseMetadata Metadata;

        public FeatureVectorView(NativeSlice<float> features, PoseMetadata metadata)
        {
            Features = features;
            Metadata = metadata;
        }

        /// <summary>
        /// 두 FeatureVector 간 L2 거리(제곱합). 채널 가중치 미적용 raw 거리.
        /// </summary>
        public static float SquaredDistance(in FeatureVectorView a, in FeatureVectorView b)
        {
            float sum = 0f;
            int len = a.Features.Length;
            for (int i = 0; i < len; i++)
            {
                float d = a.Features[i] - b.Features[i];
                sum += d * d;
            }
            return sum;
        }

        /// <summary>
        /// 채널 가중치를 적용한 가중 L2 거리. Schema의 채널 오프셋/가중치 정보가 필요하다.
        /// </summary>
        public static float WeightedSquaredDistance(
            in FeatureVectorView a, in FeatureVectorView b,
            PoseSearchSchema schema)
        {
            float total = 0f;
            for (int c = 0; c < schema.Channels.Count; c++)
            {
                int offset = schema.GetChannelOffset(c);
                int dim    = schema.Channels[c].FeatureDimension;
                float w    = schema.GetNormalizedWeight(c);

                float channelCost = 0f;
                for (int i = offset; i < offset + dim; i++)
                {
                    float d = a.Features[i] - b.Features[i];
                    channelCost += d * d;
                }
                total += channelCost * w;
            }
            return total;
        }
    }
}
