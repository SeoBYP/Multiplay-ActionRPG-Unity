using System;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.System.MotionSystem
{
    /// <summary>
    /// 현재 입력과 캐릭터 상태로 예측한 미래 root trajectory입니다.
    /// PoseFinder는 이 예측값과 baked FeatureData의 future trajectory를 비교해 다음 pose를 고릅니다.
    /// </summary>
    [Serializable]
    public struct FuturePrediction
    {
        /// <summary>미래 sample별 world position입니다.</summary>
        public NativeArray<float3> futurePositions;
        /// <summary>미래 sample별 world rotation입니다.</summary>
        public NativeArray<quaternion> futureGlobalRotations;
        /// <summary>미래 sample별 world facing direction입니다.</summary>
        public NativeArray<float3> futureDirections;
        /// <summary>strafe 모드에서 사용할 이동 방향 기반 미래 direction입니다.</summary>
        public NativeArray<float3> futureStrafingDirections;
        /// <summary>현재 root 기준 local-space 미래 위치 offset입니다.</summary>
        public NativeArray<float3> futureOffsets;
        /// <summary>현재 root 기준 local-space 미래 direction입니다.</summary>
        public NativeArray<float3> futureOffsetDirections;

        /// <summary>
        /// future prediction sample 개수에 맞춰 NativeArray 버퍼를 생성합니다.
        /// </summary>
        public void Create(int futurePredictions)
        {
            futurePositions = new NativeArray<float3>(futurePredictions, Allocator.Persistent);
            futureDirections = new NativeArray<float3>(futurePredictions, Allocator.Persistent);
            futureStrafingDirections = new NativeArray<float3>(futurePredictions, Allocator.Persistent);
            futureGlobalRotations = new NativeArray<quaternion>(futurePredictions, Allocator.Persistent);
            futureOffsets = new NativeArray<float3>(futurePredictions, Allocator.Persistent);
            futureOffsetDirections = new NativeArray<float3>(futurePredictions, Allocator.Persistent);
        }

        public override string ToString()
        {
            return "[" + futurePositions[0] + futurePositions[1] + futurePositions[2] + "] - [" + futureDirections[0] +
                   futureDirections[1] + futureDirections[2] + "]";
        }

        /// <summary>
        /// Create에서 생성한 NativeArray 버퍼를 해제합니다.
        /// </summary>
        public void Destroy()
        {
            futurePositions.Dispose();
            futureDirections.Dispose();
            futureStrafingDirections.Dispose();
            futureGlobalRotations.Dispose();
            futureOffsets.Dispose();
            futureOffsetDirections.Dispose();
        }
    }
}
