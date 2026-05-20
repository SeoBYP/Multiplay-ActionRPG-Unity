using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;

namespace Script.System.MotionSystemV2
{
    /// <summary>
    /// Feature Vector 배열로부터 KDTree를 빌드한다.
    /// 각 노드는 분산이 가장 큰 차원으로 분할하고, 중앙값(Median)을 기준으로 좌/우를 나눈다.
    /// 런타임 초기화 시 1회 실행된다. (에디터 Bake 파이프라인과 무관)
    /// </summary>
    public static class KDTreeBuilder
    {
        /// <summary>
        /// features 배열에서 KDTree를 빌드해 KDTreeNode 배열을 반환한다.
        /// </summary>
        /// <param name="features">평탄화된 Feature 배열 (포즈 수 × featureDim)</param>
        /// <param name="poseCount">총 포즈 수</param>
        /// <param name="featureDim">포즈 1개당 float 수 (schema.TotalFeatureDimension)</param>
        public static KDTreeNode[] Build(NativeArray<float> features, int poseCount, int featureDim)
        {
            if (poseCount <= 0 || featureDim <= 0)
                return Array.Empty<KDTreeNode>();

            var nodes   = new List<KDTreeNode>(poseCount * 2);
            var indices = new int[poseCount];
            for (int i = 0; i < poseCount; i++) indices[i] = i;

            BuildNode(features, featureDim, indices, 0, poseCount, nodes);
            return nodes.ToArray();
        }

        // ── Private ───────────────────────────────────────────────────────────

        private static int BuildNode(
            NativeArray<float> features, int featureDim,
            int[] indices, int start, int end,
            List<KDTreeNode> nodes)
        {
            int nodeIdx = nodes.Count;
            nodes.Add(default); // 자리 예약. 재귀 호출 후 채움

            if (end - start == 1)
            {
                nodes[nodeIdx] = new KDTreeNode
                {
                    splitDim  = -1,
                    left      = -1,
                    right     = -1,
                    poseIndex = indices[start]
                };
                return nodeIdx;
            }

            int splitDim = FindWidestVarianceDim(features, featureDim, indices, start, end);

            // 해당 차원 값으로 서브배열 정렬 후 중앙값 기준 분할
            Array.Sort(indices, start, end - start,
                Comparer<int>.Create((a, b) =>
                    features[a * featureDim + splitDim]
                        .CompareTo(features[b * featureDim + splitDim])));

            int   mid        = start + (end - start) / 2;
            float splitValue = features[indices[mid] * featureDim + splitDim];

            int left  = BuildNode(features, featureDim, indices, start, mid, nodes);
            int right = BuildNode(features, featureDim, indices, mid,   end, nodes);

            nodes[nodeIdx] = new KDTreeNode
            {
                splitDim   = splitDim,
                splitValue = splitValue,
                left       = left,
                right      = right,
                poseIndex  = -1
            };
            return nodeIdx;
        }

        /// <summary>
        /// 분산(최대-최소 스프레드)이 가장 넓은 차원을 반환한다.
        /// 균형 잡힌 분할을 위해 사이클 방식보다 데이터 기반 분할이 효과적이다.
        /// </summary>
        private static int FindWidestVarianceDim(
            NativeArray<float> features, int featureDim,
            int[] indices, int start, int end)
        {
            int   bestDim    = 0;
            float bestSpread = -1f;

            for (int d = 0; d < featureDim; d++)
            {
                float min = float.MaxValue;
                float max = float.MinValue;

                for (int i = start; i < end; i++)
                {
                    float v = features[indices[i] * featureDim + d];
                    if (v < min) min = v;
                    if (v > max) max = v;
                }

                float spread = max - min;
                if (spread > bestSpread)
                {
                    bestSpread = spread;
                    bestDim    = d;
                }
            }
            return bestDim;
        }
    }
}
