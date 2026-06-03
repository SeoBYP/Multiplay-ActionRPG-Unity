using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Presentation.InGame;
using Game.Network.Socket;
using Game.Network.Socket.Packets;
using Game.System.Player;
using NUnit.Framework;

namespace Game.Tests.EditMode.InGame
{
    /// <summary>
    /// InGameModel(MVI) 단위 테스트.
    ///
    /// 검증 포인트:
    ///   - ReturnToLobby 인텐트 → 즉시 IsReturning 상태 전환
    ///   - LeaveRoomAsync(C_PlayerLeave) → DisconnectAsync 순서 보장
    ///   - 처리 중 재진입 무시
    ///
    /// 씬 로드(SceneManager.LoadSceneAsync)는 EditMode에서 실행할 수 없으므로,
    /// Fake의 DisconnectAsync를 "완료되지 않는 UniTask"로 만들어
    /// Effect가 씬 로드 직전에 suspend되도록 한다. 그 시점까지의 동작만 검증한다.
    /// </summary>
    [TestFixture]
    public class InGameModelTests
    {
        /// <summary>
        /// 호출 순서를 기록하고, DisconnectAsync에서 의도적으로 suspend하는 Fake.
        /// </summary>
        private sealed class FakeSocketSession : ISocketSession
        {
            public readonly List<string> Calls = new();
            private readonly UniTaskCompletionSource _disconnectGate = new();

            public SocketSessionState State => default;

            public UniTask ConnectAsync(SocketConnectionInfo connectionInfo, CancellationToken ct)
            {
                Calls.Add("connect");
                return UniTask.CompletedTask;
            }

            public UniTask JoinRoomAsync(CancellationToken ct)
            {
                Calls.Add("join");
                return UniTask.CompletedTask;
            }

            public UniTask LeaveRoomAsync(CancellationToken ct)
            {
                Calls.Add("leave");
                return UniTask.CompletedTask;
            }

            public UniTask SendMoveAsync(C_Move packet, CancellationToken ct)
            {
                Calls.Add("move");
                return UniTask.CompletedTask;
            }

            public UniTask SendAsync(Packet packet, CancellationToken ct)
            {
                Calls.Add("send");
                return UniTask.CompletedTask;
            }

            public UniTask DisconnectAsync(CancellationToken ct)
            {
                Calls.Add("disconnect");
                // 완료시키지 않아 Effect가 SceneManager.LoadSceneAsync 도달 전 suspend된다.
                return _disconnectGate.Task;
            }
        }

        [Test]
        public void ReturnToLobby_인텐트시_IsReturning_상태가_true가_된다()
        {
            var fake = new FakeSocketSession();
            var model = new InGameModel(fake, new LocalPlayerContext());

            Assert.IsFalse(model.State.CurrentValue.IsReturning);

            model.Accept(InGameIntent.ReturnToLobby.Instance);

            Assert.IsTrue(model.State.CurrentValue.IsReturning);

            model.Dispose();
        }

        [Test]
        public void ReturnToLobby_LeaveRoom이_Disconnect보다_먼저_호출된다()
        {
            var fake = new FakeSocketSession();
            var model = new InGameModel(fake, new LocalPlayerContext());

            model.Accept(InGameIntent.ReturnToLobby.Instance);

            // LeaveRoomAsync는 동기 완료되므로 disconnect까지 진행 후 suspend된다.
            Assert.AreEqual(new[] { "leave", "disconnect" }, fake.Calls.ToArray());

            model.Dispose();
        }

        [Test]
        public void ReturnToLobby_처리중_재진입은_무시된다()
        {
            var fake = new FakeSocketSession();
            var model = new InGameModel(fake, new LocalPlayerContext());

            model.Accept(InGameIntent.ReturnToLobby.Instance);
            // 첫 호출이 disconnect에서 suspend된 상태(_isProcessing=true)에서 재진입
            model.Accept(InGameIntent.ReturnToLobby.Instance);

            // leave는 단 한 번만 호출돼야 한다.
            Assert.AreEqual(1, fake.Calls.FindAll(c => c == "leave").Count);

            model.Dispose();
        }
    }
}
