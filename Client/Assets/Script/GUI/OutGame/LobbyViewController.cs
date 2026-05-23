using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.GUI;
using Game.Input;
using Game.OutGame.DungeonLobby;
using R3;
using Script.System.Auth;
using Script.System.Startup;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using VContainer;
using VContainer.Unity;

namespace Game.GUI.OutGame
{
    /// <summary>
    /// 로비 화면 생명주기 컨트롤러 (POCO).
    ///
    /// 역할:
    ///   L키 입력 → LobbyView Addressable 로드 / 토글
    ///   NavigateToRoom 수신 → LobbyView 닫기
    ///
    /// IInputHandler.Priority = 100 (UI는 월드 인터랙션보다 우선)
    /// L키를 이 핸들러가 소비하면 하위 핸들러는 해당 프레임 L키를 받지 못한다.
    /// </summary>
    public sealed class LobbyViewController : IInputHandler, IInitializable, IAsyncStartable, IDisposable
    {
        private readonly LobbyModel         _model;
        private readonly IObjectResolver    _resolver;
        private readonly InputRouter        _router;
        private readonly IAuthService       _authService;
        private readonly StartupIntentQueue _startupQueue;


        public int Priority => 100;

        private AsyncOperationHandle<GameObject> _lobbyHandle;
        private GameObject                       _lobbyInstance;
        private bool                             _isLobbyOpen;

        private AsyncOperationHandle<GameObject> _detailHandle;
        private GameObject                       _detailInstance;

        private CancellationTokenSource          _cts;

        public LobbyViewController(
            LobbyModel         model,
            IObjectResolver    resolver,
            InputRouter        router,
            IAuthService       authService,
            StartupIntentQueue startupQueue)
        {
            _model        = model;
            _resolver     = resolver;
            _router       = router;
            _authService  = authService;
            _startupQueue = startupQueue;
        }

        public void Initialize()
        {
            Debug.Log("Initializing LobbyViewController");
            _cts = new CancellationTokenSource();
            _router.Register(this);

            // 방 입장/생성 성공 시 로비 닫고 대기실 열기
            _model.NavigateToRoom
                .Subscribe(roomId =>
                {
                    Debug.Log($"[LobbyViewController] NavigateToRoom 수신 roomId={roomId}");
                    CloseLobby();
                    OpenRoomDetailAsync().Forget();
                })
                .AddTo(_cts.Token);

            // 방에서 나간 경우 대기실 닫기 (IsInRoom true→false 전환 시에만)
            var wasInRoom = false;
            _model.State
                .Subscribe(s =>
                {
                    if (wasInRoom && !s.IsInRoom)
                        CloseRoomDetail();
                    wasInRoom = s.IsInRoom;
                })
                .AddTo(_cts.Token);

            // SocketServer 준비 완료 시 대기실 닫기 (씬 전환은 별도 처리)
            _model.NavigateToGame
                .Subscribe(args =>
                {
                    CloseRoomDetail();
                    Debug.Log($"[LobbyViewController] 게임 세션 준비 완료 — {args.Ip}:{args.Port}");
                })
                .AddTo(_cts.Token);

        }

        /// <summary>
        /// auth 완료 후 StartupIntentQueue를 소진한다.
        /// Initialize()는 auth 전에 실행되므로 큐가 비어있을 수 있다 — StartAsync()에서 처리해야 한다.
        /// </summary>
        public async UniTask StartAsync(CancellationToken ct)
        {
            await _authService.AuthenticatedAsync().AttachExternalCancellation(ct);

            Debug.Log($"[LobbyViewController] StartAsync — auth 완료, StartupIntentQueue HasPending={_startupQueue.HasPending}");

            while (_startupQueue.TryDequeue(out var startupIntent))
            {
                if (startupIntent is RestoreRoomStartupIntent restoreIntent)
                {
                    Debug.Log($"[LobbyViewController] StartupIntentQueue: RestoreRoom roomId={restoreIntent.RoomId}");
                    _model.Accept(new LobbyIntent.RestoreRoom(restoreIntent.RoomId));
                }
            }
        }

        public void Dispose()
        {
            _router.Unregister(this);
            _cts?.Cancel();
            _cts?.Dispose();
            CloseLobby();
            CloseRoomDetail();
        }

        // ── IInputHandler ─────────────────────────

        public bool TryHandle(GameInputAction action)
        {
            if (action != GameInputAction.ToggleLobby) return false;

            if (_isLobbyOpen) CloseLobby();
            else              OpenLobbyAsync().Forget();

            return true; // consumed — 다른 핸들러는 이 프레임 L키 못 받음
        }

        // ── Addressable 로드 / 해제 ──────────────────

        private async UniTaskVoid OpenLobbyAsync()
        {
            if (_isLobbyOpen) return;
            _isLobbyOpen = true;

            // 로비가 열리는 시점에 방 목록을 로드한다.
            // Initialize 시점이 아닌 여기서 호출하므로 로그인이 완료된 후 실행이 보장된다.
            _model.Accept(LobbyIntent.LoadRooms.Instance);

            _lobbyHandle = Addressables.LoadAssetAsync<GameObject>(AddressKeys.UI.LobbyView);

            // 로드 중 Dispose 되면 취소
            await _lobbyHandle.Task.AsUniTask().AttachExternalCancellation(_cts.Token);

            if (_cts.IsCancellationRequested)
            {
                if (_lobbyHandle.IsValid()) Addressables.Release(_lobbyHandle);
                _isLobbyOpen = false;
                return;
            }

            _lobbyInstance = UnityEngine.Object.Instantiate(_lobbyHandle.Result, GUIRoot.Instance.transform);
            _resolver.InjectGameObject(_lobbyInstance);
        }

        private void CloseLobby()
        {
            if (_lobbyInstance != null)
            {
                UnityEngine.Object.Destroy(_lobbyInstance);
                _lobbyInstance = null;
            }
            if (_lobbyHandle.IsValid())
                Addressables.Release(_lobbyHandle);

            _isLobbyOpen = false;
        }

        // ── RoomDetail 로드 / 해제 ───────────────────

        private async UniTaskVoid OpenRoomDetailAsync()
        {
            Debug.Log($"[LobbyViewController] OpenRoomDetailAsync 진입 — detailInstance={(object)_detailInstance ?? "null"}");
            if (_detailInstance != null) return;

            Debug.Log("[LobbyViewController] RoomDetail 로드 시작");
            try
            {
                _detailHandle = Addressables.LoadAssetAsync<GameObject>(AddressKeys.UI.RoomDetailView);
                await _detailHandle.Task.AsUniTask().AttachExternalCancellation(_cts.Token);

                if (_cts.IsCancellationRequested)
                {
                    if (_detailHandle.IsValid()) Addressables.Release(_detailHandle);
                    return;
                }

                _detailInstance = UnityEngine.Object.Instantiate(_detailHandle.Result, GUIRoot.Instance.transform);
                _resolver.InjectGameObject(_detailInstance);
                Debug.Log("[LobbyViewController] RoomDetail 로드 완료");
            }
            catch (Exception e)
            {
                Debug.LogError($"[LobbyViewController] RoomDetail 로드 실패: {e.Message}");
                if (_detailHandle.IsValid()) Addressables.Release(_detailHandle);
            }
        }

        private void CloseRoomDetail()
        {
            if (_detailInstance != null)
            {
                UnityEngine.Object.Destroy(_detailInstance);
                _detailInstance = null;
            }
            if (_detailHandle.IsValid())
                Addressables.Release(_detailHandle);
        }
    }
}
