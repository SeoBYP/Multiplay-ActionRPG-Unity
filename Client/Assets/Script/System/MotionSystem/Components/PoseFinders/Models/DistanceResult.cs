namespace Game.System.MotionSystem
{
    /// <summary>
    /// Pose search 결과 하나를 표현하는 값입니다.
    /// matcher는 후보 feature들의 distance를 계산한 뒤 가장 낮은 DistanceResult를 선택합니다.
    /// </summary>
    public struct DistanceResult
    {
        /// <summary>
        /// 검색 후보 배열 안에서의 결과 index입니다.
        /// </summary>
        public int index;

        /// <summary>
        /// 현재 query와 후보 pose 사이의 거리 점수입니다.
        /// 값이 낮을수록 현재 상황에 더 잘 맞는 pose입니다.
        /// </summary>
        public float distance;

        /// <summary>
        /// 선택된 pose frame index입니다.
        /// Dataset의 animation frame 조회나 continuity 처리에 사용할 수 있습니다.
        /// </summary>
        public int pose;

        /// <summary>
        /// 이 결과가 속한 feature 검색 범위입니다.
        /// query 내부의 부분 범위 검색이나 action 구간 제한에 사용할 수 있습니다.
        /// </summary>
        public QueryRange queryRange;
    }
}
