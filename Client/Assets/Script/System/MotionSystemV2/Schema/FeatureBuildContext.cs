using System;
using Unity.Mathematics;
using UnityEngine;

namespace Script.System.MotionSystemV2
{
    /// <summary>
    /// DatabaseBuilder가 AnimationClip에서 Feature를 추출할 때 채널에 전달하는 데이터 묶음.
    /// 채널은 이 컨텍스트로 해당 프레임의 본 위치/속도, 궤적을 읽어 Feature Vector를 채운다.
    /// </summary>
    public struct FeatureBuildContext
    {
        // 샘플링 중인 AnimationClip
        public AnimationClip clip;

        // 현재 샘플링 시간 (초)
        public float sampleTime;

        // 전체 클립 길이 (초)
        public float clipLength;

        // 샘플링 간격 (PoseStep = 1 / SampleRate)
        public float poseStep;

        // 루트 Transform (캐릭터 월드 위치/회전 기준)
        public Transform rootTransform;

        // 루트 기준 world-to-local 행렬 (Feature는 항상 캐릭터 로컬 스페이스로 저장)
        public float4x4 rootInverse;

        // 스켈레톤의 모든 본 Transform 배열 (HumanBodyBones 인덱스 기준)
        public Transform[] boneTransforms;

        // 이전 프레임 본 위치 (속도 계산용)
        public float3[] prevBonePositions;

        /// <summary>
        /// 특정 시간의 루트 월드 위치를 반환하는 델리게이트.
        /// Humanoid 클립에서 AnimationClip.SampleAnimation은 루트 모션을
        /// characterGO.transform에 올바르게 적용하지 못하므로,
        /// DatabaseBuilder가 PlayableGraph 기반 샘플러를 주입한다.
        /// null이면 TrajectoryChannel이 기존 SampleAnimation 방식으로 fallback한다.
        /// </summary>
        public Func<float, Vector3> sampleRootPosition;
    }
}
