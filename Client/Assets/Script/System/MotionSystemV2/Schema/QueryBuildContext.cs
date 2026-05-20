using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Script.System.MotionSystemV2
{
    /// <summary>
    /// 런타임에 MotionMatchingComponent가 채널에 전달하는 현재 캐릭터 상태 묶음.
    /// 채널은 이 컨텍스트에서 쿼리 Feature Vector를 채운다.
    /// </summary>
    public struct QueryBuildContext
    {
        public Transform rootTransform;
        public float4x4 rootInverse;

        // HumanBodyBones 인덱스 기준 본 Transform 배열
        public Transform[] boneTransforms;

        // 캐릭터 로컬 스페이스 본 위치 / 속도
        public NativeArray<float3> bonePositions;
        public NativeArray<float3> boneVelocities;

        // TrajectoryChannel이 사용하는 궤적 히스토리 (timeOffset 오름차순 정렬)
        public NativeArray<TrajectorySampleData> trajectoryHistory;

        public float deltaTime;
    }
}
