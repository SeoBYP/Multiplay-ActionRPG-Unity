using Unity.Collections;

namespace Game.System.MotionSystem
{
    /// <summary>
    /// pose distance 계산에서 bone feature, future trajectory, past trajectory가 얼마나 중요한지 나타내는 가중치 묶음입니다.
    /// QueryComputedFlow.ManageWeights가 query tag의 Characteristics 설정을 평균내서 이 값을 구성합니다.
    /// </summary>
    public struct BonesWeights
    {
        public NativeArray<float> weights;
        public float weightFutureOffset;
        public float weightFutureDirection;
        public float weightPastOffset;
        public float weightPastDirection;
        public float totalWeightPositions;
        public float totalWeightVelocities;
    }
}
