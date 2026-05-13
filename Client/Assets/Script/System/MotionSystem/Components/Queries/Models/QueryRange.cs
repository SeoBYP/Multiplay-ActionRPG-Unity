using System;

namespace Game.System.MotionSystem
{
    /// <summary>
    /// QueryComputed.featuresData 안에서 검색할 feature index 범위를 나타냅니다.
    /// 전체 query를 모두 검색하지 않고 특정 action 구간, loop 구간, transition 구간만 제한 검색할 때 사용합니다.
    /// </summary>
    [Serializable]
    public struct QueryRange
    {
        /// <summary>
        /// 검색을 시작할 feature index입니다.
        /// </summary>
        public int featureIDStart;

        /// <summary>
        /// 검색을 끝낼 feature index입니다.
        /// </summary>
        public int featureIDStop;

        /// <summary>
        /// 시작/끝 feature index를 지정해 검색 범위를 만듭니다.
        /// </summary>
        public QueryRange(int featureIDStart, int featureIDStop)
        {
            this.featureIDStart = featureIDStart;
            this.featureIDStop = featureIDStop;
        }
    }
}
