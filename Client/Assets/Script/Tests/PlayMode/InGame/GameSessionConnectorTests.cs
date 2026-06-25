using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Network.Socket;
using Game.Network.Socket.Packets;
using Game.System.Auth;
using Game.System.DungeonLobby;
using Game.System.GameScene;
using GameServer.Grpc.DungeonLobby;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.InGame
{
    /// <summary>
    /// GameSessionConnector 단위 테스트 (PlayMode).
    ///
    /// 핵심 검증:
    ///   - JoinRoomAsync 응답 대기 중 연결이 끊겨 Disconnected 상태가 되어도
    ///     무한 대기(WaitUntil 루프)에 빠지지 않고 재시도해야 한다.
    ///   - Joined 상태 도달 시 정상 흐름으로 빠져나가야 한다.
    ///   - 중복 GameSessionReady 이벤트는 무시해야 한다.
    ///
    /// PlayMode인 이유: 커넥터가 UniTask.WaitUntil/Delay를, 테스트가 UniTask.DelayFrame을
    /// 사용한다. UniTask의 PlayerLoopHelper는 [RuntimeInitializeOnLoadMethod]로 초기화되어
    /// Play 모드에서만 동작한다. EditMode에서는 PlayerLoop이 null이라 NRE가 난다.
    /// (Docker 불필요 — Fake 기반 순수 단위 테스트다.)
    /// </summary>
    [TestFixture]
    public class GameSessionConnectorTests
    {
        // ── Fake ──────────────────────────────────────────────────────

        private sealed class FakeGameSceneManager : IGameSceneManager
        {
            public UniTask LoadSceneAsync(string sceneName, CancellationToken ct = default, global::System.Func<UniTask> holdUntil = null)
                => UniTask.CompletedTask;
        }

        private sealed class FakeSocketSession : ISocketSession
        {
            private readonly Queue<SocketSessionState> _stateQueue = new Queue<SocketSessionState>();
            private SocketSessionState _currentState = SocketSessionState.Idle;

            public SocketSessionState State => _currentState;
            public event global::System.Action OnDisconnected { add { } remove { } }
            public int ConnectCallCount { get; private set; }
            public int JoinCallCount    { get; private set; }

            public FakeSocketSession QueueState(SocketSessionState state)
            {
                _stateQueue.Enqueue(state);
                return this;
            }

            public UniTask ConnectAsync(SocketConnectionInfo connectionInfo, CancellationToken ct)
            {
                ConnectCallCount++;
                _currentState = SocketSessionState.Connected;
                return UniTask.CompletedTask;
            }

            public UniTask JoinRoomAsync(CancellationToken ct)
            {
                JoinCallCount++;
                _currentState = _stateQueue.Count > 0
                    ? _stateQueue.Dequeue()
                    : SocketSessionState.Failed;
                return UniTask.CompletedTask;
            }

            public UniTask LeaveRoomAsync(CancellationToken ct) => UniTask.CompletedTask;

            public UniTask DisconnectAsync(CancellationToken ct)
            {
                _currentState = SocketSessionState.Disconnected;
                return UniTask.CompletedTask;
            }

            public UniTask SendMoveAsync(C_Move packet, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask SendAsync(Packet packet, CancellationToken ct) => UniTask.CompletedTask;
        }

        private sealed class FakeDungeonLobbyService : IDungeonLobbyService
        {
            public bool IsInRoom     => false;
            public RoomInfo? CurrentRoom => null;

            public event Action<RoomInfo>?          OnRoomUpdated    { add { } remove { } }
            public event Action<RoomInfo>?          OnGameStarting   { add { } remove { } }
            public event Action<string, int, long>? OnGameSessionReady;

            public void FireGameSessionReady(string ip, int port, long roomId)
                => OnGameSessionReady?.Invoke(ip, port, roomId);

            public UniTask<(DungeonLobbyResult, IReadOnlyList<RoomInfo>)> GetRoomsAsync(CancellationToken ct = default)
                => UniTask.FromResult<(DungeonLobbyResult, IReadOnlyList<RoomInfo>)>((DungeonLobbyResult.Success, Array.Empty<RoomInfo>()));

            public UniTask<DungeonLobbyResult> CreateRoomAsync(string roomName, int maxPlayers, string mapId = "", CancellationToken ct = default)
                => UniTask.FromResult(DungeonLobbyResult.Success);
            public UniTask<DungeonLobbyResult> JoinRoomAsync(long roomId, CancellationToken ct = default)
                => UniTask.FromResult(DungeonLobbyResult.Success);
            public UniTask<DungeonLobbyResult> LeaveRoomAsync(CancellationToken ct = default)
                => UniTask.FromResult(DungeonLobbyResult.Success);
            public UniTask<DungeonLobbyResult> StartGameAsync(CancellationToken ct = default)
                => UniTask.FromResult(DungeonLobbyResult.Success);
            public UniTask<DungeonLobbyResult> RestoreRoomAsync(long roomId, CancellationToken ct = default)
                => UniTask.FromResult(DungeonLobbyResult.Success);
        }

        // ── 헬퍼 ─────────────────────────────────────────────────────

        private static AuthSession MakeAuthSession()
        {
            var session = new AuthSession();
            session.Update("fake-access", "fake-refresh", DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds());
            return session;
        }

        /// <summary>
        /// ConnectAsync가 expected회 호출될 때까지 대기한다.
        /// 커넥터는 재시도 사이에 JoinRetryDelay(500ms)를 두므로 고정 프레임 대기로는 부족하다.
        /// 버그로 재시도가 안 되면 무한 대기에 빠지지 않도록 타임아웃으로 끊는다(끊겨도 이후 Assert가 실제 횟수를 드러낸다).
        /// </summary>
        private static async UniTask WaitForConnectAttemptsAsync(FakeSocketSession session, int expected)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            try
            {
                await UniTask.WaitUntil(() => session.ConnectCallCount >= expected, cancellationToken: cts.Token);
            }
            catch (OperationCanceledException)
            {
                // 타임아웃 — 재시도가 일어나지 않은 경우. Assert가 실제 횟수로 실패 메시지를 보여준다.
            }
        }

        // ── 테스트 ───────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator JoinRoomAsync_대기_중_Disconnected_상태면_재시도한다() => UniTask.ToCoroutine(async () =>
        {
            // Arrange
            // 1차 → Disconnected (연결 끊김), 2차 → Joined (성공)
            var session = new FakeSocketSession()
                .QueueState(SocketSessionState.Disconnected)
                .QueueState(SocketSessionState.Joined);

            var lobbyService = new FakeDungeonLobbyService();
            var connector    = new Game.System.InGame.GameSessionConnector(lobbyService, session, MakeAuthSession(), new FakeGameSceneManager(), new SocketPacketState());
            connector.Initialize();

            // Act
            lobbyService.FireGameSessionReady("127.0.0.1", 7777, 1);

            // 재시도 사이에 JoinRetryDelay(500ms)가 있으므로 고정 프레임 대기로는 2차 시도를 못 본다.
            // 2회 시도가 일어날 때까지 대기하되, 무한 대기 방지를 위해 타임아웃으로 묶는다.
            await WaitForConnectAttemptsAsync(session, 2);

            // Assert — Disconnected(1차) + Joined(2차) = 2번 시도
            Assert.AreEqual(2, session.ConnectCallCount, "Disconnected 상태 시 재시도해야 한다");
            Assert.AreEqual(2, session.JoinCallCount);

            connector.Dispose();
        });

        [UnityTest]
        public IEnumerator JoinRoomAsync_Failed_상태면_재시도한다() => UniTask.ToCoroutine(async () =>
        {
            // 1차 → Failed (방 없음), 2차 → Joined
            var session = new FakeSocketSession()
                .QueueState(SocketSessionState.Failed)
                .QueueState(SocketSessionState.Joined);

            var lobbyService = new FakeDungeonLobbyService();
            var connector    = new Game.System.InGame.GameSessionConnector(lobbyService, session, MakeAuthSession(), new FakeGameSceneManager(), new SocketPacketState());
            connector.Initialize();

            lobbyService.FireGameSessionReady("127.0.0.1", 7777, 1);
            await WaitForConnectAttemptsAsync(session, 2);

            Assert.AreEqual(2, session.ConnectCallCount, "Failed 상태 시 재시도해야 한다");

            connector.Dispose();
        });

        [UnityTest]
        public IEnumerator GameSessionReady_이미_연결_중이면_무시된다() => UniTask.ToCoroutine(async () =>
        {
            var session = new FakeSocketSession()
                .QueueState(SocketSessionState.Joined);

            var lobbyService = new FakeDungeonLobbyService();
            var connector    = new Game.System.InGame.GameSessionConnector(lobbyService, session, MakeAuthSession(), new FakeGameSceneManager(), new SocketPacketState());
            connector.Initialize();

            lobbyService.FireGameSessionReady("127.0.0.1", 7777, 1);
            lobbyService.FireGameSessionReady("127.0.0.1", 7777, 1); // 중복

            await UniTask.DelayFrame(5);

            Assert.AreEqual(1, session.ConnectCallCount, "중복 이벤트는 무시해야 한다");

            connector.Dispose();
        });
    }
}
