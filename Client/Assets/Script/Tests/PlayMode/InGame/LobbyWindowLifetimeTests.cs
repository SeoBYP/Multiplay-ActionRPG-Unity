using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Gameplay.Input;
using Game.GUI;
using Game.GUI.OutGame;
using Game.GUI.OutGame.Lobby;
using Game.Presentation.DungeonLobby;
using Game.System.Auth;
using Game.System.DungeonLobby;
using GameServer.Grpc.DungeonLobby;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode.InGame
{
    /// <summary>
    /// 로비(방 목록)·대기실 창의 **수명**.
    ///
    /// 이 창들은 `GUIRoot`(DontDestroyOnLoad) 아래에 만들어진다 — 즉 **씬 전환이 치워 주지 않는다.**
    /// 누가 파괴하는지 필드 하나(`_lobbyInst`)에 달려 있는데, 로드가 프레임을 넘기는 동안 요청이 겹치면
    /// 인스턴스가 두 개 생기고 필드는 마지막 것만 가리켜 **앞의 것이 미아**로 남는다
    /// (실측: 던전에 들어가도 방 목록이 화면에 남아 있었다).
    /// </summary>
    [TestFixture]
    public class LobbyWindowLifetimeTests
    {
        private FakeDungeonLobbyService _lobbyService;
        private LobbyModel _model;
        private LobbyViewController _controller;
        private IObjectResolver _resolver;
        private PlayerInputActions _actions;
        private InputRouter _router;
        private GameObject _guiRoot;

        [SetUp]
        public void SetUp()
        {
            _guiRoot = new GameObject("GUIRoot", typeof(RectTransform), typeof(Canvas), typeof(GUIRoot));

            _lobbyService = new FakeDungeonLobbyService();
            _model = new LobbyModel(
                new LobbyRepository(_lobbyService),
                _lobbyService,
                new FakeAuthService(),
                new UserProfile(),
                new Script.System.Startup.StartupIntentQueue(),
                new NoopInputContext());

            _actions = new PlayerInputActions();
            _router  = new InputRouter(_actions);

            var builder = new ContainerBuilder();
            builder.RegisterInstance(_model);
            _resolver = builder.Build();

            _controller = new LobbyViewController(_model, _resolver, _router);
            _controller.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            _controller.Dispose();
            _resolver.Dispose();

            _actions.Disable();
            if (_actions.asset != null) Object.DestroyImmediate(_actions.asset);

            foreach (var stray in Object.FindObjectsByType<DungeonRoomLobbyView>(FindObjectsInactive.Include))
                Object.DestroyImmediate(stray.gameObject);

            if (_guiRoot != null) Object.DestroyImmediate(_guiRoot);
        }

        [UnityTest]
        public IEnumerator 열기_요청이_겹쳐도_방목록_인스턴스는_하나다() => UniTask.ToCoroutine(async () =>
        {
            // 같은 프레임에 두 번(연타·중복 신호) — 로드가 끝나기 전이라 예전엔 두 개가 만들어졌다.
            _controller.TryHandle(GameInputAction.ToggleLobby);
            _controller.TryHandle(GameInputAction.ToggleLobby);

            await WaitForLobbyAsync();

            Assert.AreEqual(1, CountLobbyViews(), "로비 창이 두 개 만들어지면 앞의 것은 아무도 파괴하지 못한다.");
        });

        [UnityTest]
        public IEnumerator 던전에_입장하면_방목록이_사라진다() => UniTask.ToCoroutine(async () =>
        {
            _controller.TryHandle(GameInputAction.ToggleLobby);
            await WaitForLobbyAsync();
            Assert.AreEqual(1, CountLobbyViews(), "사전 조건: 로비가 떠 있어야 한다.");

            _lobbyService.RaiseGameSessionReady("127.0.0.1", 7777, roomId: 1); // 던전 입장 신호
            await UniTask.NextFrame();

            Assert.AreEqual(0, CountLobbyViews(), "던전에 들어가면 방 목록은 화면에서 없어져야 한다.");
        });

        [UnityTest]
        public IEnumerator 로드_도중_던전에_입장해도_창이_남지_않는다() => UniTask.ToCoroutine(async () =>
        {
            // 로드가 끝나기 전에 입장 신호가 오는 경우 — 완료된 인스턴스가 스스로 치워야 한다.
            _controller.TryHandle(GameInputAction.ToggleLobby);
            _lobbyService.RaiseGameSessionReady("127.0.0.1", 7777, roomId: 1);

            for (int i = 0; i < 120 && CountLobbyViews() == 0; i++)
                await UniTask.NextFrame();      // 로드가 끝날 시간을 준다
            for (int i = 0; i < 10; i++)
                await UniTask.NextFrame();      // 끝난 뒤 스스로 치울 시간

            Assert.AreEqual(0, CountLobbyViews(), "로드 도중 닫힌 창은 완료 시점에 스스로 파괴돼야 한다.");
        });

        // ── 헬퍼 ────────────────────────────────

        private static int CountLobbyViews()
            => Object.FindObjectsByType<DungeonRoomLobbyView>(FindObjectsInactive.Include).Length;

        private static async UniTask WaitForLobbyAsync()
        {
            for (int i = 0; i < 180 && CountLobbyViews() == 0; i++)
                await UniTask.NextFrame();
            // 겹쳐 만들어진 두 번째 인스턴스까지 드러나도록 몇 프레임 더 본다.
            for (int i = 0; i < 10; i++)
                await UniTask.NextFrame();
        }

        // ── Fakes ───────────────────────────────

        private sealed class FakeDungeonLobbyService : IDungeonLobbyService
        {
            public bool IsInRoom => CurrentRoom != null;
            public RoomInfo CurrentRoom { get; private set; }

            public event Action<RoomInfo> OnRoomUpdated { add { } remove { } }
            public event Action<RoomInfo> OnGameStarting { add { } remove { } }
            public event Action<string, int, long> OnGameSessionReady;

            public void RaiseGameSessionReady(string ip, int port, long roomId)
                => OnGameSessionReady?.Invoke(ip, port, roomId);

            public UniTask<(DungeonLobbyResult, IReadOnlyList<RoomInfo>, int TotalCount)> GetRoomsAsync(
                int offset = 0, int limit = 20, CancellationToken ct = default)
                => UniTask.FromResult((DungeonLobbyResult.Success, (IReadOnlyList<RoomInfo>)new List<RoomInfo>(), 0));

            public UniTask<DungeonLobbyResult> CreateRoomAsync(string roomName, int maxPlayers, string mapId = "", CancellationToken ct = default)
                => UniTask.FromResult(DungeonLobbyResult.Success);
            public UniTask<DungeonLobbyResult> JoinRoomAsync(long roomId, CancellationToken ct = default)
                => UniTask.FromResult(DungeonLobbyResult.Success);
            public UniTask<DungeonLobbyResult> LeaveRoomAsync(CancellationToken ct = default)
                => UniTask.FromResult(DungeonLobbyResult.Success);
            public UniTask<DungeonLobbyResult> StartGameAsync(CancellationToken ct = default)
                => UniTask.FromResult(DungeonLobbyResult.Success);
            public UniTask<DungeonLobbyResult> SetReadyAsync(bool isReady, CancellationToken ct = default)
                => UniTask.FromResult(DungeonLobbyResult.Success);
            public UniTask<DungeonLobbyResult> RestoreRoomAsync(long roomId, CancellationToken ct = default)
                => UniTask.FromResult(DungeonLobbyResult.Success);
        }

        private sealed class FakeAuthService : IAuthService
        {
            public bool IsAuthenticated => true;
            public UniTask AuthenticatedAsync() => UniTask.CompletedTask;
            public UniTask<AuthResult> TryAutoLoginAsync(CancellationToken ct) => UniTask.FromResult(AuthResult.Success);
            public UniTask<AuthResult> LoginOrRegisterAsync(string email, string password, CancellationToken ct) => UniTask.FromResult(AuthResult.Success);
            public void Logout() { }
            public UniTask<AuthResult> RefreshTokenAsync(CancellationToken ct) => UniTask.FromResult(AuthResult.Success);
        }

        private sealed class NoopInputContext : Game.System.Input.IInputContext
        {
            public void EnterUi() { }
            public void ExitUi() { }
            public bool IsUiActive => false;
        }
    }
}
