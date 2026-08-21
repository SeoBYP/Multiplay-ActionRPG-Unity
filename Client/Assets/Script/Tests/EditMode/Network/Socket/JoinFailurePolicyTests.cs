using Game.Network.Socket;
using NUnit.Framework;

namespace Game.Tests.EditMode.Network.Socket
{
    /// <summary>
    /// 방 입장 실패 사유 분기 — <b>재시도해서 풀릴 실패</b>와 <b>기다려도 안 풀릴 실패</b>를 가른다.
    ///
    /// 배경(실측): 예전엔 서버가 보낸 사유(<c>S_PlayerJoined.Message</c>)를 버리고 `state=Failed` 만 찍어서,
    /// 콘솔에 경고 30줄이 쌓여도 원인을 알 수 없었다(실제로 "Room is full" 이었는데 로그로는 알 수 없었다).
    /// 사유를 남기는 것 자체가 진단이고, 그중 회복 불가한 것은 30번 헛돌지 말고 즉시 끝내야 한다.
    /// </summary>
    public class JoinFailurePolicyTests
    {
        [Test]
        public void 방이_가득참은_재시도_대상이다()
        {
            // 끊긴 옛 세션이 자리를 물고 있는 동안 뜬다 — 서버 인수(takeover)나 타임아웃으로 곧 풀린다.
            Assert.IsFalse(JoinFailurePolicy.IsTerminal("Room is full"));
        }

        [Test]
        public void 방_없음과_상태_없음은_재시도_대상이다()
        {
            // 게임 시작 직후엔 방 생성·상태 초기화가 아직 안 끝났을 수 있다(정상 레이스).
            Assert.IsFalse(JoinFailurePolicy.IsTerminal("Room not found"));
            Assert.IsFalse(JoinFailurePolicy.IsTerminal("Player state not initialized"));
            Assert.IsFalse(JoinFailurePolicy.IsTerminal("Failed to join room"));
        }

        [Test]
        public void 배정_불일치는_즉시_포기한다()
        {
            // 내가 배정받지 않은 방에 들어가려는 것 — 재시도로는 절대 안 바뀐다.
            Assert.IsTrue(JoinFailurePolicy.IsTerminal("Room assignment mismatch"));
            Assert.IsTrue(JoinFailurePolicy.IsTerminal("Player not assigned to any session"));
        }

        [Test]
        public void 사유를_모르면_재시도한다()
        {
            // 모르는 문자열에 즉시 포기하면, 서버 문구가 바뀌었을 때 조용히 기능이 죽는다. 기본값은 안전한 쪽.
            Assert.IsFalse(JoinFailurePolicy.IsTerminal(null));
            Assert.IsFalse(JoinFailurePolicy.IsTerminal(string.Empty));
            Assert.IsFalse(JoinFailurePolicy.IsTerminal("무언가 새로운 사유"));
        }
    }
}
