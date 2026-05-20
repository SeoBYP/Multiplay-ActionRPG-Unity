using Unity.Collections;
using Unity.Mathematics;

namespace Script.System.MotionSystemV2
{
    /// <summary>
    /// 전수 탐색 검색 전략.
    /// 모든 포즈와 쿼리 간 가중 L2 거리를 계산해 최솟값을 선택한다.
    /// KDTree 챕터 전까지 기본 검색으로 사용한다.
    /// </summary>
    public sealed class BruteForceSearch : ISearchStrategy
    {
        public void Dispose() { } // NativeArray 없음 — no-op

        private readonly PoseSearchDatabase _database;
        private readonly PoseSearchSchema   _schema;

        public BruteForceSearch(PoseSearchDatabase database)
        {
            _database = database;
            _schema   = database.schema;
        }

        public int FindBestPose(
            NativeSlice<float> queryFeatures,
            int excludePoseIndex,
            int jumpThresholdFrames)
        {
            int   bestIndex = 0;
            float bestCost  = float.MaxValue;
            int   poseCount = _database.PoseCount;
            int   featureDim = _schema.TotalFeatureDimension;

            for (int i = 0; i < poseCount; i++)
            {
                // 현재 재생 중인 클립 내 인접 프레임 점프 방지
                if (excludePoseIndex >= 0 && IsTooClose(i, excludePoseIndex, jumpThresholdFrames))
                    continue;

                float cost = ComputeCost(queryFeatures, i, featureDim);

                // BlockTransition 포즈는 외부에서 직접 점프 불가
                if (_database.NativeMetadata[i].HasFlag(PoseFlags.BlockTransition))
                    continue;

                if (cost < bestCost)
                {
                    bestCost  = cost;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private float ComputeCost(NativeSlice<float> query, int poseIndex, int featureDim)
        {
            float total = 0f;
            var   db    = _database.NativeFeatures;

            for (int c = 0; c < _schema.Channels.Count; c++)
            {
                int   offset = _schema.GetChannelOffset(c);
                int   dim    = _schema.Channels[c].FeatureDimension;
                float w      = _schema.GetNormalizedWeight(c);

                float channelCost = 0f;
                int   base_       = poseIndex * featureDim + offset;

                for (int j = 0; j < dim; j++)
                {
                    float d = query[offset + j] - db[base_ + j];
                    channelCost += d * d;
                }

                total += channelCost * w;
            }

            return total;
        }

        /// <summary>
        /// 같은 클립 내에서 현재 프레임과 너무 가까운 포즈는 점프하지 않는다.
        /// </summary>
        private bool IsTooClose(int candidateIndex, int currentIndex, int thresholdFrames)
        {
            if (thresholdFrames <= 0) return false;

            var candidateMeta = _database.NativeMetadata[candidateIndex];
            var currentMeta   = _database.NativeMetadata[currentIndex];

            if (candidateMeta.entryIndex != currentMeta.entryIndex) return false;

            return math.abs(candidateMeta.frameIndex - currentMeta.frameIndex) <= thresholdFrames;
        }
    }
}
