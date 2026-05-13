using Unity.Collections;
using Unity.Mathematics;

namespace Game.System.MotionSystem
{
    /// <summary>
    /// Pose blending/inertialization 단계에서 이전 frame의 bone 상태와 보정 offset을 보관하는 런타임 데이터입니다.
    /// 선택된 pose를 즉시 덮어쓰지 않고 현재 움직임 흐름을 이어가려면 마지막 위치, 회전, 속도 정보가 필요합니다.
    /// </summary>
    public struct MotionData
    {
        /// <summary>
        /// inertialization 또는 pose offset 보정에 사용할 bone별 offset입니다.
        /// </summary>
        public NativeArray<OffsetBone> Offsets;

        /// <summary>
        /// 직전 frame의 bone rotation 배열입니다.
        /// </summary>
        public NativeArray<quaternion> LastRotations;

        /// <summary>
        /// 직전 frame의 bone position 배열입니다.
        /// </summary>
        public NativeArray<float3> LastPositions;

        /// <summary>
        /// 직전 frame의 bone velocity 배열입니다.
        /// </summary>
        public NativeArray<float3> LastVelocities;

        /// <summary>
        /// 직전 frame의 scale velocity 배열입니다.
        /// </summary>
        public NativeArray<float3> LastVelocityScales;

        /// <summary>
        /// 직전 frame의 bone scale 배열입니다.
        /// </summary>
        public NativeArray<float3> LastScales;

        /// <summary>
        /// 직전 frame 기준 bone angular velocity 배열입니다.
        /// </summary>
        public NativeArray<float3> AngularVelocities;

        /// <summary>
        /// 현재 frame보다 한 단계 이전의 bone position 배열입니다.
        /// velocity 계산 또는 pose continuity 비교에 사용할 수 있습니다.
        /// </summary>
        public NativeArray<float3> PreviousPositions;
    }

    /// <summary>
    /// 하나의 bone에 적용할 inertialization/offset 보정 값입니다.
    /// target pose로 전환할 때 기존 pose의 움직임 잔차를 일정 시간 줄여나가는 용도로 확장할 수 있습니다.
    /// </summary>
    public struct OffsetBone
    {
        /// <summary>
        /// rotation offset입니다.
        /// </summary>
        public quaternion rotation;

        /// <summary>
        /// angular velocity offset입니다.
        /// </summary>
        public float3 angularVelocity;

        /// <summary>
        /// position offset입니다.
        /// </summary>
        public float3 position;

        /// <summary>
        /// velocity offset입니다.
        /// </summary>
        public float3 velocity;

        /// <summary>
        /// scale offset입니다.
        /// </summary>
        public float3 scale;

        /// <summary>
        /// scale velocity offset입니다.
        /// </summary>
        public float3 scaleVelocity;
    }
}
