using System;

namespace Script.System.MotionSystemV2
{
    /// <summary>
    /// 특정 시간에 한 번 발생하는 포인트 Notify.
    /// MotionNotifyTrack에서 리스트로 관리한다.
    /// </summary>
    [Serializable]
    public class MotionNotify
    {
        /// <summary>외부에서 수신할 이벤트 식별자 (예: "FootStep_L", "Attack_Start")</summary>
        public string name;

        /// <summary>클립 내 발생 시간 (초). 클립 재생 시간과 비교해 트리거한다.</summary>
        public float time;
    }
}
