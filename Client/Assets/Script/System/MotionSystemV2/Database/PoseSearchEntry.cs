using System;
using UnityEngine;

namespace Script.System.MotionSystemV2
{
    /// <summary>
    /// PoseSearchDatabase에 등록되는 AnimationClip 항목.
    /// 클립별 CostBias와 트리밍 범위를 설정한다.
    /// </summary>
    [Serializable]
    public class PoseSearchEntry
    {
        [Tooltip("검색 대상 AnimationClip. 루트 모션 활성화 필요")]
        public AnimationClip clip;

        /// <summary>
        /// Inspector/Gizmos 표시용 이름. FBX 내부 클립명이 'Unreal Take' 등 제네릭일 때
        /// Setup 스크립트가 FBX 파일명으로 자동 채운다.
        /// </summary>
        [Tooltip("Inspector·Gizmos 표시 이름. Setup 스크립트가 자동 설정 (FBX 파일명)")]
        public string clipDisplayName;

        [Tooltip("루핑 애니메이션 여부. 활성화 시 마지막 프레임에서 첫 프레임으로 자연스럽게 연결")]
        public bool isLooping;

        [Tooltip("이 클립이 지속 선택될 때 추가되는 Cost 바이어스. 음수=오래 유지, 양수=빠른 교체")]
        public float continuingPoseCostBias = 0f;

        [Tooltip("클립 시작에서 제외할 시간 (초). 어색한 시작 프레임 제거용")]
        [Min(0f)] public float trimStart = 0f;

        [Tooltip("클립 끝에서 제외할 시간 (초). 어색한 종료 프레임 제거용")]
        [Min(0f)] public float trimEnd = 0f;

        [Tooltip("이 클립에 연결된 Notify 트랙 (없으면 null)")]
        public MotionNotifyTrack notifyTrack;

        public float EffectiveDuration => clip != null
            ? Mathf.Max(0f, clip.length - trimStart - trimEnd)
            : 0f;

        public float StartTime => trimStart;
        public float EndTime   => clip != null ? clip.length - trimEnd : 0f;
    }
}
