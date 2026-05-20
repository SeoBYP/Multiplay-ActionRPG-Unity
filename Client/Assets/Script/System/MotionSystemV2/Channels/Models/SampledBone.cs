using System;
using UnityEngine;

namespace Script.System.MotionSystemV2
{
    /// <summary>
    /// PoseChannel Inspector에서 설정하는 샘플링 본 항목.
    /// 로코모션에서는 LeftFoot / RightFoot 권장.
    /// </summary>
    [Serializable]
    public struct SampledBone
    {
        [Tooltip("샘플링할 본")]
        public HumanBodyBones bone;

        [Range(0f, 1f), Tooltip("이 본이 채널 내 Cost에 미치는 영향력")]
        public float weight;
    }
}
