using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Presentation.InGame;
using Game.Network.Socket;
using Game.Network.Socket.Packets;
using Game.System.Input;
using Game.System.Player;
using NUnit.Framework;
using R3;

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
            public event global::System.Action OnDisconnected;
            public void RaiseDisconnected() => OnDisconnected?.Invoke();

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

        /// <summary>입력 점유 push/pop 횟수만 세는 Fake IInputContext.</summary>
        private sealed class FakeInputContext : IInputContext
        {
            public int EnterCount { get; private set; }
            public int ExitCount { get; private set; }
            public bool IsUiActive => EnterCount > ExitCount;
            public void EnterUi() => EnterCount++;
            public void ExitUi() => ExitCount++;
        }

        [Test]
        public void 소켓_비정상끊김시_입력정지하고_OnConnectionLost가_발행된다()
        {
            var fake = new FakeSocketSession();
            var input = new FakeInputContext();
            var model = new InGameModel(fake, new LocalPlayerContext(), inputContext: input);
            model.Initialize(); // OnDisconnected 구독

            bool lost = false;
            using var sub = model.OnConnectionLost.Subscribe(_ => lost = true);

            fake.RaiseDisconnected();

            Assert.IsTrue(lost, "끊김 시 OnConnectionLost 발행");
            Assert.AreEqual(1, input.EnterCount, "끊김 시 입력 정지(EnterUi)");

            fake.RaiseDisconnected(); // 중복 끊김 — 1회만 처리
            Assert.AreEqual(1, input.EnterCount, "중복 끊김은 무시(EnterUi 재호출 X)");

            model.Dispose();
            Assert.AreEqual(1, input.ExitCount, "Dispose 시 입력 점유 해제(ExitUi)로 균형");
        }

        [Test]
        public void 아이템_획득시_OnItemPickup_토스트_메시지가_발행된다()
        {
            // 줍기 애니 대체: 서버 S_ItemPickedUp → InGameModel.OnItemPickup(메시지) → GameHud 토스트.
            var fake = new FakeSocketSession();
            var state = new SocketPacketState();
            var model = new InGameModel(fake, new LocalPlayerContext(), packetState: state);
            model.Initialize();

            string msg = null;
            using var sub = model.OnItemPickup.Subscribe(m => msg = m.Message);

            state.NotifyItemPickedUp(1001, 2);
            Assert.AreEqual("1001 x2 획득", msg, "복수 획득은 수량을 표기해야 한다(표시명 카탈로그 없으면 numericId 를 그대로 보여준다).");

            state.NotifyItemPickedUp(2101, 1);
            Assert.AreEqual("2101 획득", msg, "단일 획득은 수량을 생략해야 한다.");

            model.Dispose();
        }

        [Test]
        public void Main_로컬줍기_통지시_OnItemPickup_토스트가_발행된다()
        {
            // Main(비네트워크) 줍기: LocalGroundItem → ItemPickupNotifier → InGameModel(던전 소켓과 동일 토스트 병합).
            var fake = new FakeSocketSession();
            var notifier = new ItemPickupNotifier();
            var model = new InGameModel(fake, new LocalPlayerContext(), pickupNotifier: notifier);
            model.Initialize();

            string msg = null;
            using var sub = model.OnItemPickup.Subscribe(m => msg = m.Message);

            notifier.Notify(3001, 3);
            Assert.AreEqual("3001 x3 획득", msg, "Main 로컬 줍기도 동일 획득 토스트로 병합돼야 한다.");

            model.Dispose();
        }

        [Test]
        public void 상호작용_대상이_바뀌면_안내문구가_발행되고_대상이_없으면_숨김이_내려간다()
        {
            // 탐지(Gameplay) → InteractionPromptNotifier → InGameModel → HUD. 여기선 모델 중계만 검증한다.
            var fake = new FakeSocketSession();
            var notifier = new InteractionPromptNotifier();
            var model = new InGameModel(fake, new LocalPlayerContext(), promptNotifier: notifier);
            model.Initialize();

            var received = new List<string>();
            using var sub = model.OnInteractionPrompt.Subscribe(p => received.Add(p));

            notifier.Set("오르기");
            notifier.Set("오르기");   // 같은 대상 재통지 — 중복 발행하지 않아야 한다
            notifier.Set(null);       // 대상에서 벗어남 → 숨김

            CollectionAssert.AreEqual(new[] { "오르기", null }, received,
                "대상이 바뀔 때만 발행돼야 한다(매 프레임 폴링이라 중복이 그대로 새면 UI 가 떤다).");

            model.Dispose();
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
