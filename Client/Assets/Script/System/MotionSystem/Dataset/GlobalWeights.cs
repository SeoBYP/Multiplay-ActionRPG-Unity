namespace Game.System.MotionSystem
{
    /// <summary>
    /// pose search distance의 큰 항목별 전역 가중치입니다.
    /// Query tag에서 나온 BonesWeights가 세부 feature 가중치라면, 이 값은 bone/future/past 그룹 전체의 영향도를 조절합니다.
    /// </summary>
    public struct GlobalWeights
    {
        public float weightBonesPosition;
        public float weightBonesVelocity;
        public float weightFutureRootPosition;
        public float weightFutureRootDirection;
        public float weightPastRootPosition;
        public float weightPastRootDirection;
        public float weightBones;
        public float weightFutures;
        public float weightpasts;
    }
}
