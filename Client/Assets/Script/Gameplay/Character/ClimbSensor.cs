using UnityEngine;

namespace Game.Gameplay.Character
{
    /// <summary>
    /// 사다리 부착 신호 보관소(P6) — 플레이어에 붙는다. <see cref="GroundedDetector"/> 와 같은 역할:
    /// <b>상태는 여기 있고, 전이 규칙이 폴링</b>한다(FSM 이 MonoBehaviour 를 직접 알지 않게).
    ///
    /// 흐름: <see cref="Ladder.Interact"/> → <see cref="RequestAttach"/> → GroundState 의 전이 규칙이
    /// <see cref="ConsumeAttach"/> 로 소비 → ClimbState 진입. 이탈 판정도 여기서 답한다(사다리 상/하단 도달).
    /// </summary>
    public sealed class ClimbSensor : MonoBehaviour
    {
        /// <summary>붙기로 한 사다리. 부착 중에도 유지되며 ClimbState 가 이동 축·이탈 판정에 쓴다.</summary>
        public Ladder Current { get; private set; }

        private bool _requested;
        private bool _releaseRequested;  // 바닥 근처에서 아래 입력 → 내려서기
        private bool _jumpOffRequested;  // Space → 반대쪽으로 뛰어내리기

        /// <summary>사다리가 "나에게 붙어라"라고 요청(상호작용 시). 한 번만 소비된다.</summary>
        public void RequestAttach(Ladder ladder)
        {
            if (ladder == null) return;
            Current = ladder;
            _requested = true;
        }

        /// <summary>부착 요청을 소비한다(one-shot — 입력 규약과 동일). 전이 규칙이 매 프레임 폴링.</summary>
        public bool ConsumeAttach()
        {
            if (!_requested) return false;
            _requested = false;
            return true;
        }

        /// <summary>바닥 근처에서 "그냥 내려서기" 요청(아래 입력). 다음 전이 판정에서 소비된다.</summary>
        public void RequestRelease() => _releaseRequested = true;

        /// <summary>점프 이탈 요청(Space). 사다리를 밀어내며 낙하 상태로 빠진다.</summary>
        public void RequestJumpOff() => _jumpOffRequested = true;

        /// <summary>점프 이탈이 요청됐는가(전이 규칙이 폴링, ClimbState.Exit 가 밀어내기에 사용).</summary>
        public bool JumpOffRequested => _jumpOffRequested;

        /// <summary>사다리에서 손을 뗄 때(이탈 완료) 호출 — 참조와 잔여 요청을 정리한다.</summary>
        public void Release()
        {
            Current = null;
            _requested = false;
            _releaseRequested = false;
            _jumpOffRequested = false;
        }

        /// <summary>
        /// 지금 사다리를 벗어나야 하는가. 상단 도달(위로 올라섬) 또는 하단 도달(발이 땅)이면 true.
        /// 어느 쪽인지는 <paramref name="atTop"/> 로 알려 ClimbState 가 상단 이탈 텔레포트를 결정한다.
        /// </summary>
        public bool ShouldDetach(Vector3 playerPosition, out bool atTop)
        {
            atTop = false;
            if (Current == null) return true; // 사다리가 사라졌으면(파괴 등) 즉시 이탈
            if (_releaseRequested) return true; // 바닥 근처 아래 입력 → 내려서기(위로 올라선 게 아니므로 atTop=false)

            if (playerPosition.y >= Current.TopY)
            {
                atTop = true;
                return true;
            }
            return playerPosition.y <= Current.BottomY;
        }
    }
}
