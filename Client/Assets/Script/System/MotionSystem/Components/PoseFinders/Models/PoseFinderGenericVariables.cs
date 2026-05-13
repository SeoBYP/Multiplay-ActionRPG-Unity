using Unity.Collections;
using Unity.Mathematics;

namespace Game.System.MotionSystem
{
    /// <summary>
    /// Pose finder가 매 검색마다 재사용할 임시 NativeArray 묶음입니다.
    /// 매 frame 새 배열을 만들면 GC와 native allocation 비용이 커지므로,
    /// MotionMatching 초기화 시 한 번 만들고 검색 단계에서 계속 재사용합니다.
    /// </summary>
    public struct PoseFinderGenericVariables
    {
        /// <summary>
        /// 현재 query 기준으로 정규화된 future facing direction 배열입니다.
        /// </summary>
        public NativeArray<float3> normalizedFutureDirections;

        /// <summary>
        /// 현재 query 기준으로 정규화된 future root offset 배열입니다.
        /// </summary>
        public NativeArray<float3> normalizedFutureOffsets;

        /// <summary>
        /// pose history 기준으로 정규화된 past facing direction 배열입니다.
        /// </summary>
        public NativeArray<float3> normalizedPastDirections;

        /// <summary>
        /// pose history 기준으로 정규화된 past root offset 배열입니다.
        /// </summary>
        public NativeArray<float3> normalizedPastOffsets;

        /// <summary>
        /// 현재 runtime skeleton의 bone position을 검색용으로 복사해둘 배열입니다.
        /// </summary>
        public NativeArray<float3> currentPositions;

        /// <summary>
        /// 현재 runtime pose에서 계산한 position/velocity feature 배열입니다.
        /// bone마다 position과 velocity를 함께 저장하기 위해 bonesLength * 2 크기를 사용합니다.
        /// </summary>
        public NativeArray<float3> currentFeatures;

        /// <summary>
        /// Dataset 설정과 bone 개수에 맞춰 pose finder 임시 버퍼를 생성합니다.
        /// </summary>
        public void Create(int futureEstimates, int pastEstimates, int bonesLength)
        {
            normalizedFutureDirections = new NativeArray<float3>(futureEstimates, Allocator.Persistent);
            normalizedFutureOffsets = new NativeArray<float3>(futureEstimates, Allocator.Persistent);
            normalizedPastDirections = new NativeArray<float3>(pastEstimates, Allocator.Persistent);
            normalizedPastOffsets = new NativeArray<float3>(pastEstimates, Allocator.Persistent);
            currentPositions = new NativeArray<float3>(bonesLength, Allocator.Persistent);
            currentFeatures = new NativeArray<float3>(bonesLength * 2, Allocator.Persistent);
        }

        /// <summary>
        /// Create에서 만든 NativeArray를 해제합니다.
        /// MotionMatching 종료 시 반드시 호출해야 합니다.
        /// </summary>
        public void Destroy()
        {
            normalizedFutureDirections.Dispose();
            normalizedFutureOffsets.Dispose();
            normalizedPastDirections.Dispose();
            normalizedPastOffsets.Dispose();
            currentPositions.Dispose();
            currentFeatures.Dispose();
        }
    }
}
