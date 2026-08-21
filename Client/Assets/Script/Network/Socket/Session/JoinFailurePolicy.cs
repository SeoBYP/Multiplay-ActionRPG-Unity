namespace Game.Network.Socket
{
    /// <summary>
    /// 방 입장 거절 사유의 회복 가능성 판정 — 순수 함수(테스트로 고정).
    ///
    /// 서버는 거절할 때 <c>S_PlayerJoined.Message</c> 에 사유를 담는다. 그중
    /// <b>기다리면 풀리는 것</b>(방 생성 중·상태 초기화 중·옛 세션이 자리 물고 있음)과
    /// <b>기다려도 안 풀리는 것</b>(내가 배정받지 않은 방)을 갈라야 한다.
    /// 후자에 30번 재시도하는 것은 시간 낭비이자 로그 오염이다.
    ///
    /// <b>모르는 사유는 재시도</b>가 기본값이다 — 서버 문구가 바뀌었을 때 조용히 포기하는 쪽이 더 나쁘다.
    /// </summary>
    public static class JoinFailurePolicy
    {
        /// <summary>재시도해도 바뀌지 않는 사유인가.</summary>
        public static bool IsTerminal(string reason)
        {
            if (string.IsNullOrEmpty(reason)) return false;

            // 배정 자체가 어긋난 경우 — 재접속하든 기다리든 같은 결과다.
            return reason.Contains("assignment mismatch")
                || reason.Contains("not assigned");
        }
    }
}
