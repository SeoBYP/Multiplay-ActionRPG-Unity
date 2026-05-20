using System;
using UnityEngine;

namespace Script.System.MotionSystemV2
{
    /// <summary>
    /// Schema Inspector에서 설정하는 궤적 샘플 포인트.
    /// timeOffset 음수 = 과거, 양수 = 미래.
    /// </summary>
    [Serializable]
    public struct TrajectorySample
    {
        [Tooltip("샘플 시간 오프셋 (초). 음수=과거, 양수=미래")]
        public float timeOffset;

        [Range(0f, 1f), Tooltip("이 샘플의 Cost 기여 가중치")]
        public float weight;
    }
}
