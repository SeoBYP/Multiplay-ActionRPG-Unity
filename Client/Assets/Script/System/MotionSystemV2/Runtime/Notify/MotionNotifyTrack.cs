using System.Collections.Generic;
using UnityEngine;

namespace Script.System.MotionSystemV2
{
    /// <summary>
    /// PoseSearchEntry에 연결하는 Notify 컨테이너.
    /// 포인트 이벤트(MotionNotify)와 구간 이벤트(MotionNotifyState)를 모두 담는다.
    /// </summary>
    [CreateAssetMenu(fileName = "NotifyTrack", menuName = "MotionMatchingV2/Notify Track")]
    public class MotionNotifyTrack : ScriptableObject
    {
        [Tooltip("특정 시간에 한 번 발생하는 포인트 이벤트 목록")]
        [SerializeField] private List<MotionNotify>      _notifies      = new();

        [Tooltip("시작~종료 구간 동안 지속되는 상태형 이벤트 목록")]
        [SerializeField] private List<MotionNotifyState> _notifyStates  = new();

        public IReadOnlyList<MotionNotify>      Notifies      => _notifies;
        public IReadOnlyList<MotionNotifyState> NotifyStates  => _notifyStates;

#if UNITY_EDITOR
        private void OnValidate()
        {
            foreach (var ns in _notifyStates)
            {
                if (ns.endTime < ns.startTime)
                    ns.endTime = ns.startTime;
            }
        }
#endif
    }
}
