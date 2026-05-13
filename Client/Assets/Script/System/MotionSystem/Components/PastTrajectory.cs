using Unity.Collections;
using Unity.Mathematics;

namespace Game.System.MotionSystem
{

    /// <summary>
    /// 최근 root position/direction 기록입니다.
    /// PoseFinder는 이 값을 baked past trajectory와 비교해 현재 pose가 이전 움직임과 자연스럽게 이어지는지 판단합니다.
    /// </summary>
    public struct PastTrajectory
    {
        /// <summary>과거 sample별 world facing direction입니다.</summary>
        public NativeArray<float3> pastGlobalDirection;
        /// <summary>과거 sample별 world position입니다.</summary>
        public NativeArray<float3> pastGlobalPosition;

        /// <summary>
        /// past trajectory sample 버퍼를 생성하고 초기 direction을 채웁니다.
        /// </summary>
        public void Create(int pastEstimates, float3 forward)
        {
            pastGlobalDirection = new NativeArray<float3>(pastEstimates, Allocator.Persistent);
            pastGlobalPosition = new NativeArray<float3>(pastEstimates, Allocator.Persistent);

            for (int i = 0; i < pastEstimates; i++)
            {
                pastGlobalDirection[i] = forward;
                pastGlobalPosition[i] = float3.zero;
            }
        }

        public override string ToString()
        {
            return "[" + pastGlobalDirection[0] + pastGlobalDirection[1] + pastGlobalDirection[2] + "] - [" +
                   pastGlobalPosition[0] +
                   pastGlobalPosition[1] + pastGlobalPosition[2] + "]";
        }

        /// <summary>
        /// Create에서 생성한 NativeArray 버퍼를 해제합니다.
        /// </summary>
        public void Destroy()
        {
            pastGlobalDirection.Dispose();
            pastGlobalPosition.Dispose();
        }
    }
}
