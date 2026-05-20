using System;
using System.Collections.Generic;
using UnityEngine.Playables;

namespace Script.System.MotionSystemV2
{
    /// <summary>
    /// MotionMatchingComponent — Notify 처리 파트 (partial).
    /// 클립 시간 기반으로 MotionNotify(포인트)와 MotionNotifyState(구간)를 매 프레임 처리한다.
    ///
    /// 포즈 점프 시 동작:
    ///   - 진행 중인 NotifyState는 End로 강제 종료
    ///   - 점프 후 새 clipTime 기준으로 재시작
    /// </summary>
    public sealed partial class MotionMatchingComponent
    {
        // ── 이벤트 ───────────────────────────────────────────────────────────────
        /// <summary>포인트 Notify 발생 시. 인자: notify.name</summary>
        public event Action<string> OnNotify;

        /// <summary>NotifyState 구간 진입 시</summary>
        public event Action<string> OnNotifyStateBegin;

        /// <summary>NotifyState 구간 내 매 FixedUpdate</summary>
        public event Action<string> OnNotifyStateTick;

        /// <summary>NotifyState 구간 이탈 시 (포즈 점프에 의한 강제 종료 포함)</summary>
        public event Action<string> OnNotifyStateEnd;

        // ── 내부 상태 ────────────────────────────────────────────────────────────
        // ApplyPose에서 설정, ProcessNotifies에서 소비
        private bool  _pendingJump;
        private float _prevClipTime;

        // 현재 활성화된 NotifyState 이름 집합
        private readonly HashSet<string> _activeNotifyStates = new();

        // ── 처리 ─────────────────────────────────────────────────────────────────

        private void ProcessNotifies()
        {
            if (!_activePlayable.IsValid()) return;

            float currTime = (float)_activePlayable.GetTime();

            // 포즈 점프: 활성 구간 Notify 강제 종료
            if (_pendingJump)
            {
                foreach (var name in _activeNotifyStates)
                    OnNotifyStateEnd?.Invoke(name);
                _activeNotifyStates.Clear();
                _pendingJump = false;
                // _prevClipTime은 ApplyPose에서 이미 meta.clipTime으로 리셋됨
            }

            var track = _currentEntry?.notifyTrack;
            if (track == null)
            {
                _prevClipTime = currTime;
                return;
            }

            // ── 포인트 Notify ─────────────────────────────────────────────────
            // prevClipTime < t <= currClipTime 범위에 있는 Notify 트리거
            foreach (var n in track.Notifies)
            {
                if (n.time > _prevClipTime && n.time <= currTime)
                    OnNotify?.Invoke(n.name);
            }

            // ── 구간 NotifyState ──────────────────────────────────────────────
            foreach (var ns in track.NotifyStates)
            {
                bool wasActive = _activeNotifyStates.Contains(ns.name);
                bool isActive  = ns.Contains(currTime);

                if (!wasActive && isActive)
                {
                    _activeNotifyStates.Add(ns.name);
                    OnNotifyStateBegin?.Invoke(ns.name);
                }
                else if (wasActive && isActive)
                {
                    OnNotifyStateTick?.Invoke(ns.name);
                }
                else if (wasActive && !isActive)
                {
                    _activeNotifyStates.Remove(ns.name);
                    OnNotifyStateEnd?.Invoke(ns.name);
                }
            }

            _prevClipTime = currTime;
        }
    }
}
