using System;

namespace Script.System.MotionSystemV2
{
    /// <summary>
    /// startTime ~ endTime 구간 동안 지속되는 상태형 Notify.
    /// 구간 진입 시 Begin, 구간 내 매 프레임 Tick, 구간 이탈 시 End 이벤트가 발생한다.
    /// 포즈 점프로 구간이 건너뛰어지면 End가 강제로 발생한다.
    /// </summary>
    [Serializable]
    public class MotionNotifyState
    {
        /// <summary>외부에서 수신할 이벤트 식별자 (예: "Invincible", "AttackWindow")</summary>
        public string name;

        /// <summary>구간 시작 시간 (초)</summary>
        public float startTime;

        /// <summary>구간 종료 시간 (초). startTime보다 커야 한다.</summary>
        public float endTime;

        public bool Contains(float clipTime) => clipTime >= startTime && clipTime <= endTime;
    }
}
