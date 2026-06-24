using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Network.Socket;
using Game.Network.Socket.Packets;
using Game.Presentation.InGame;
using Game.System.Player;
using Game.System.Progression;
using NUnit.Framework;

namespace Game.Tests.EditMode.InGame
{
    /// <summary>
    /// PlayerProgressionHolder → InGameModel → InGameState exp 릴레이 단위 테스트 (EditMode).
    ///
    /// 검증 포인트:
    ///   - 홀더 Refresh 시 현재 레벨/Exp/ExpToNext 가 State 에 반영(HUD exp 게이지의 소스)
    ///   - 홀더가 없으면(던전 외 등) State 는 초기값(exp 게이지 미갱신)
    ///
    /// 게이지 렌더 자체는 GameHud(PlayMode)에서 검증. 여기선 순수 데이터 흐름만 본다.
    /// </summary>
    [TestFixture]
    public class InGameExpRelayTests
    {
        private InGameModel _model;

        [TearDown]
        public void TearDown()
        {
            _model?.Dispose();
            _model = null;
        }

        [Test]
        public void 홀더_Refresh시_레벨과_Exp가_State에_반영된다()
        {
            var service = new FakeProgressionService(level: 3, exp: 40, expToNext: 100);
            var holder = new PlayerProgressionHolder(service);
            _model = new InGameModel(new FakeSocketSession(), new LocalPlayerContext(),
                null, null, null, holder);
            _model.Initialize();

            holder.RefreshAsync().GetAwaiter().GetResult();

            var state = _model.State.CurrentValue;
            Assert.AreEqual(3, state.Level);
            Assert.AreEqual(40, state.Exp);
            Assert.AreEqual(100, state.ExpToNext);
        }

        [Test]
        public void 홀더가_없으면_State_Exp는_초기값이다()
        {
            _model = new InGameModel(new FakeSocketSession(), new LocalPlayerContext());
            _model.Initialize();

            var state = _model.State.CurrentValue;
            Assert.AreEqual(0, state.Level);
            Assert.AreEqual(0, state.Exp);
            Assert.AreEqual(0, state.ExpToNext);
        }

        // ── 헬퍼 ────────────────────────────────────────

        private sealed class FakeProgressionService : IProgressionService
        {
            private readonly ProgressionData _data;
            public FakeProgressionService(int level, long exp, long expToNext)
                => _data = new ProgressionData(level, exp, expToNext, 60, default);

            public UniTask<(ProgressionResult Result, ProgressionData Data)> GetProgressionAsync(CancellationToken ct = default)
                => UniTask.FromResult((ProgressionResult.Success, _data));
        }

        private sealed class FakeSocketSession : ISocketSession
        {
            public SocketSessionState State => default;
            public event global::System.Action OnDisconnected { add { } remove { } }
            public UniTask ConnectAsync(SocketConnectionInfo connectionInfo, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask JoinRoomAsync(CancellationToken ct) => UniTask.CompletedTask;
            public UniTask LeaveRoomAsync(CancellationToken ct) => UniTask.CompletedTask;
            public UniTask SendMoveAsync(C_Move packet, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask SendAsync(Packet packet, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask DisconnectAsync(CancellationToken ct) => UniTask.CompletedTask;
        }
    }
}
