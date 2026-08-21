using Game.Gameplay.Character;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode.Gameplay
{
    /// <summary>
    /// 던전 원격 로코모션 동기화의 순수 계산부.
    ///
    /// <b>왜 방향을 패킷에 안 싣는가</b>: 원격은 이미 위치(→속도)와 <c>RotY</c>(→facing)를 받는다.
    /// 둘을 조합하면 로컬이 <see cref="GroundState"/> 에서 쓰는 것과 <b>같은 공식</b>으로 MoveX/MoveY 를
    /// 복원할 수 있다 — 즉 방향은 이미 전송되고 있는 정보라 추가 바이트가 필요 없다.
    /// (반면 점프·낙하·사다리는 y 변화가 서로 같아 복원 불가 → 그것만 1바이트로 싣는다.)
    /// </summary>
    public class RemoteLocomotionTests
    {
        [Test]
        public void 정면을_볼_때_오른쪽_이동은_MoveX로_분해된다()
        {
            // rotY=0 → forward=+Z, right=+X. 월드 +X 로 2.3m/s 이동.
            var mv = RemoteLocomotion.ToFacingFrame(new Vector3(2.3f, 0f, 0f), rotYDegrees: 0f);

            Assert.AreEqual(2.3f, mv.x, 0.001f, "오른쪽 이동은 MoveX 에 실려야 한다(게걸음).");
            Assert.AreEqual(0f, mv.y, 0.001f, "전후 성분은 없어야 한다.");
        }

        [Test]
        public void 바라보는_방향으로_이동하면_MoveY_양수다()
        {
            // rotY=90 → forward=+X. 월드 +X 이동 = 전진.
            var mv = RemoteLocomotion.ToFacingFrame(new Vector3(3.3f, 0f, 0f), rotYDegrees: 90f);

            Assert.AreEqual(3.3f, mv.y, 0.001f, "바라보는 쪽 이동은 MoveY 양수(전진).");
            Assert.AreEqual(0f, mv.x, 0.001f);
        }

        [Test]
        public void 뒤로_이동하면_MoveY_음수다()
        {
            var mv = RemoteLocomotion.ToFacingFrame(new Vector3(0f, 0f, -2.3f), rotYDegrees: 0f);

            Assert.Less(mv.y, -2f, "후진은 MoveY 음수여야 뒷걸음 클립이 나온다.");
        }

        [Test]
        public void 수직_성분은_무시된다()
        {
            // 점프·낙하의 y 는 로코모션 블렌드와 무관하다(그건 AnimState 가 담당).
            var mv = RemoteLocomotion.ToFacingFrame(new Vector3(0f, 9f, 2.3f), rotYDegrees: 0f);

            Assert.AreEqual(2.3f, mv.y, 0.001f);
            Assert.AreEqual(0f, mv.x, 0.001f);
        }

        [Test]
        public void 단위는_정규화가_아니라_실측_m_per_s다()
        {
            // 컨트롤러 2D 블렌드 좌표가 클립 실측 속도(m/s)라 정규화하면 발이 미끄러진다.
            var walk = RemoteLocomotion.ToFacingFrame(new Vector3(0f, 0f, 2.3f), 0f);
            var run = RemoteLocomotion.ToFacingFrame(new Vector3(0f, 0f, 3.3f), 0f);

            Assert.AreEqual(2.3f, walk.y, 0.001f);
            Assert.AreEqual(3.3f, run.y, 0.001f, "빠르면 값도 커져야 한다(정규화면 둘 다 1이 된다).");
        }

        [Test]
        public void AnimState_바이트_매핑은_서버_테스트_상수와_일치한다()
        {
            // 서버는 이 값을 해석하지 않고 릴레이만 하므로 enum 을 두지 않는다(불투명 byte).
            // 의미의 진실원은 이 enum 이고, SocketServer.Tests/Packets 의 상수(0·1·4)와 짝을 이룬다.
            Assert.AreEqual(0, (byte)StateKind.Ground);
            Assert.AreEqual(1, (byte)StateKind.Jump);
            Assert.AreEqual(2, (byte)StateKind.Fall);
            Assert.AreEqual(3, (byte)StateKind.Land);
            Assert.AreEqual(4, (byte)StateKind.Climb);
        }
    }
}
