using Unity.Mathematics;

namespace Script.System.MotionSystemV2
{
    /// <summary>
    /// 런타임 궤적 히스토리의 특정 시점 데이터. NativeArray에 저장된다.
    /// timeOffset 음수 = 과거, 양수 = 미래.
    /// position/direction은 캐릭터 로컬 스페이스 기준.
    /// </summary>
    public struct TrajectorySampleData
    {
        public float timeOffset;
        public float3 position;    // 캐릭터 로컬 스페이스
        public float3 direction;   // 정규화된 이동 방향
    }
}
