using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Script.System.MotionSystemV2
{
    /// <summary>
    /// KDTree 기반 근사 최근접 포즈 검색.
    /// BruteForce 대비 O(log N) 탐색으로 대규모 Database에서 성능을 확보한다.
    ///
    /// 근사 허용 오차(epsilon): 이미 발견한 최적 비용의 (1+ε) 배보다 멀리 있는
    /// 서브트리는 탐색하지 않는다. epsilon=0이면 정확한 NN, 양수면 탐색 범위 축소.
    /// </summary>
    public sealed class KDTreeSearch : ISearchStrategy
    {
        // 근사 허용 오차. 0.02 = 정확 해보다 최대 2% 비쌀 수 있는 포즈까지 허용
        private const float Epsilon = 0.02f;

        private readonly PoseSearchDatabase _database;
        private readonly PoseSearchSchema   _schema;
        private          NativeArray<KDTreeNode> _nodes;

        public KDTreeSearch(PoseSearchDatabase database)
        {
            _database = database;
            _schema   = database.schema;

            var nodeArray = KDTreeBuilder.Build(
                database.NativeFeatures,
                database.PoseCount,
                database.schema.TotalFeatureDimension);

            _nodes = new NativeArray<KDTreeNode>(nodeArray, Allocator.Persistent);

            Debug.Log($"[KDTree] Built — {_nodes.Length} nodes for {database.PoseCount} poses");
        }

        public void Dispose()
        {
            if (_nodes.IsCreated) _nodes.Dispose();
        }

        public int FindBestPose(
            NativeSlice<float> queryFeatures,
            int excludePoseIndex,
            int jumpThresholdFrames)
        {
            if (!_nodes.IsCreated || _nodes.Length == 0) return 0;

            int   bestPose   = -1;
            float bestCost   = float.MaxValue;
            int   featureDim = _schema.TotalFeatureDimension;

            // 스택 엔트리: (nodeIndex, 이 서브트리까지 쿼리의 최소 거리²)
            // 최대 깊이 = log2(poseCount) ≈ 10(1024포즈 기준). 64면 충분
            unsafe
            {
                var stack = stackalloc StackEntry[64];
                int top   = 0;
                stack[top++] = new StackEntry { nodeIndex = 0, minDistSq = 0f };

                while (top > 0)
                {
                    var entry = stack[--top];

                    // 이 서브트리의 최소 가능 거리가 현재 최적보다 이미 크면 탐색 불필요
                    if (entry.minDistSq > bestCost * (1f + Epsilon)) continue;

                    var node = _nodes[entry.nodeIndex];

                    // ── 리프 노드 ─────────────────────────────────────────
                    if (node.splitDim < 0)
                    {
                        int pose = node.poseIndex;
                        if (_database.NativeMetadata[pose].HasFlag(PoseFlags.BlockTransition)) continue;
                        if (excludePoseIndex >= 0 && IsTooClose(pose, excludePoseIndex, jumpThresholdFrames)) continue;

                        float cost = ComputeCost(queryFeatures, pose, featureDim);
                        if (cost < bestCost)
                        {
                            bestCost = cost;
                            bestPose = pose;
                        }
                        continue;
                    }

                    // ── 내부 노드: 가까운 쪽 우선 탐색 ──────────────────
                    float qVal      = queryFeatures[node.splitDim];
                    float planeDiff = qVal - node.splitValue;
                    bool  goLeft    = planeDiff <= 0f;

                    int nearChild = goLeft ? node.left  : node.right;
                    int farChild  = goLeft ? node.right : node.left;

                    // 먼 쪽은 split 평면까지의 거리² 조건 충족 시만 스택에 추가
                    // 스택은 LIFO이므로 near를 나중에 push해 먼저 꺼냄
                    float farMinDistSq = planeDiff * planeDiff;
                    if (farChild >= 0)
                        stack[top++] = new StackEntry { nodeIndex = farChild, minDistSq = farMinDistSq };

                    if (nearChild >= 0)
                        stack[top++] = new StackEntry { nodeIndex = nearChild, minDistSq = entry.minDistSq };
                }
            }

            return bestPose >= 0 ? bestPose : 0;
        }

        // ── Private ───────────────────────────────────────────────────────────

        private float ComputeCost(NativeSlice<float> query, int poseIndex, int featureDim)
        {
            float total = 0f;
            var   db    = _database.NativeFeatures;

            for (int c = 0; c < _schema.Channels.Count; c++)
            {
                int   offset = _schema.GetChannelOffset(c);
                int   dim    = _schema.Channels[c].FeatureDimension;
                float w      = _schema.GetNormalizedWeight(c);
                int   baseDb = poseIndex * featureDim + offset;

                float channelCost = 0f;
                for (int j = 0; j < dim; j++)
                {
                    float d = query[offset + j] - db[baseDb + j];
                    channelCost += d * d;
                }
                total += channelCost * w;
            }
            return total;
        }

        private bool IsTooClose(int candidateIndex, int currentIndex, int thresholdFrames)
        {
            if (thresholdFrames <= 0) return false;
            var cand = _database.NativeMetadata[candidateIndex];
            var curr = _database.NativeMetadata[currentIndex];
            if (cand.entryIndex != curr.entryIndex) return false;
            return math.abs(cand.frameIndex - curr.frameIndex) <= thresholdFrames;
        }

        private struct StackEntry
        {
            public int   nodeIndex;
            public float minDistSq;
        }
    }
}
