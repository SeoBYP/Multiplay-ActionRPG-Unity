using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Network.Socket;
using Game.Network.Socket.Packets;
using Game.System.Auth;
using Game.System.DungeonLobby;
using GameServer.Grpc.DungeonLobby;
using NUnit.Framework;

namespace Game.Tests.EditMode.InGame
{
    /// <summary>
    /// GameSessionConnector 단위 테스트.
    ///
    /// 핵심 검증:
    ///   - JoinRoomAsync 응답 대기 중 연결이 끊겨 Disconnected 상태가 되어도
    ///     무한 대기(WaitUntil 루프)에 빠지지 않고 재시도해야 한다.
    ///   - Joined 상태 도달 시 정상 흐름으로 빠져나가야 한다.
    ///   - 중복 GameSessionReady 이벤트는 무시해야 한다.
    ///
    /// SceneManager.LoadSceneAsync는 EditMode에서 실행할 수 없으므로,
    /// Joined 상태가 되면 씬 로드 예외가 catch(Exception)로 잡혀 루프가 종료된다.
    /// ConnectCallCount로 시도 횟수만 검증한다.
    /// </summary>
    [TestFixture]
    public class GameSessionConnectorTests
    {
        // ── Fake ──────────────────────────────────────────────────────

        private sealed class FakeGameSceneManager : Game.System.GameScene.IGameSceneManager
        {
            public UniTask LoadSceneAsync(string sceneName, CancellationToken ct = default)
                => UniTask.CompletedTask;
        }

        private sealed class FakeSocketSession : ISocketSession
        {
            private readonly Queue<SocketSessionState> _stateQueue = new Queue<SocketSessionState>();
            private SocketSessionState _currentState = SocketSessionState.Idle;

            public SocketSessionState State => _currentState;
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

            public UniTask<DungeonLobbyResult> CreateRoomAsync(string roomName, int maxPlayers, CancellationToken ct = default)
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

        // ── 테스트 ───────────────────────────────────────────────────

        [Test]
        public async UniTask JoinRoomAsync_대기_중_Disconnected_상태면_재시도한다()
        {
            // Arrange
            // 1차 → Disconnected (연결 끊김), 2차 → Joined (성공)
            var session = new FakeSocketSession()
                .QueueState(SocketSessionState.Disconnected)
                .QueueState(SocketSessionState.Joined);

            var lobbyService = new FakeDungeonLobbyService();
            var connector    = new Game.System.InGame.GameSessionConnector(lobbyService, session, MakeAuthSession(), new FakeGameSceneManager());
            connector.Initialize();

            // Act
            lobbyService.FireGameSessionReady("127.0.0.1", 7777, 1);
            await UniTask.DelayFrame(5);

            // Assert — Disconnected(1차) + Joined(2차) = 2번 시도
            Assert.AreEqual(2, session.ConnectCallCount, "Disconnected 상태 시 재시도해야 한다");
            Assert.AreEqual(2, session.JoinCallCount);

            connector.Dispose();
        }

        [Test]
        public async UniTask JoinRoomAsync_Failed_상태면_재시도한다()
        {
            // 1차 → Failed (방 없음), 2차 → Joined
            var session = new FakeSocketSession()
                .QueueState(SocketSessionState.Failed)
                .QueueState(SocketSessionState.Joined);

            var lobbyService = new FakeDungeonLobbyService();
            var connector    = new Game.System.InGame.GameSessionConnector(lobbyService, session, MakeAuthSession(), new FakeGameSceneManager());
            connector.Initialize();

            lobbyService.FireGameSessionReady("127.0.0.1", 7777, 1);
            await UniTask.DelayFrame(5);

            Assert.AreEqual(2, session.ConnectCallCount, "Failed 상태 시 재시도해야 한다");

            connector.Dispose();
        }

        [Test]
        public async UniTask GameSessionReady_이미_연결_중이면_무시된다()
        {
            var session = new FakeSocketSession()
                .QueueState(SocketSessionState.Joined);

            var lobbyService = new FakeDungeonLobbyService();
            var connector    = new Game.System.InGame.GameSessionConnector(lobbyService, session, MakeAuthSession(), new FakeGameSceneManager());
            connector.Initialize();

            lobbyService.FireGameSessionReady("127.0.0.1", 7777, 1);
            lobbyService.FireGameSessionReady("127.0.0.1", 7777, 1); // 중복

            await UniTask.DelayFrame(5);

            Assert.AreEqual(1, session.ConnectCallCount, "중복 이벤트는 무시해야 한다");

            connector.Dispose();
        }
    }
}
