using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.System.DungeonLobby;
using Game.System.Input;
using GameServer.Grpc.DungeonLobby;
using R3;
using Game.System.Auth;
using Script.System.Startup;
using UnityEngine;
using VContainer.Unity;

namespace Game.Presentation.DungeonLobby
{
    /// <summary>
    /// 로비 화면의 MVI Model.
    ///
    /// Intent → Effect → Result → Reducer → State
    ///
    /// 규칙:
    ///   View는 Accept(Intent)만 호출한다.
    ///   Model은 State만 발행한다. View를 직접 조작하지 않는다.
    ///   Reducer는 순수 함수다. 비동기 처리는 Effect 메서드에서만 한다.
    /// </summary>
    public sealed class LobbyModel : IInitializable, IAsyncStartable, IDisposable
    {
        private readonly LobbyRepository       _repository;
        private readonly IDungeonLobbyService  _lobbyService;
        private readonly IAuthService          _authService;
        private readonly UserProfile           _userProfile;
        private readonly StartupIntentQueue    _startupQueue;
        private readonly IInputContext         _inputContext;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private readonly ReactiveProperty<LobbyState> _state
            = new ReactiveProperty<LobbyState>(LobbyState.Initial);

        private readonly Subject<long>           _navigateToRoom  = new Subject<long>();
        private readonly Subject<(string, int)>  _navigateToGame  = new Subject<(string, int)>();

        private bool _isProcessing;

        public ReadOnlyReactiveProperty<LobbyState> State =>
            _state.ToReadOnlyReactiveProperty();

        /// <summary>방 생성/입장 성공 시 발행.</summary>
        public Observable<long> NavigateToRoom => _navigateToRoom;

        /// <summary>SocketServer 준비 완료 시 발행 — (ip, port).</summary>
        public Observable<(string Ip, int Port)> NavigateToGame => _navigateToGame;

        public LobbyModel(
            LobbyRepository    repository,
            IDungeonLobbyService lobbyService,
            IAuthService         authService,
            UserProfile          userProfile,
            StartupIntentQueue   startupQueue,
            IInputContext        inputContext)
        {
            _repository   = repository;
            _lobbyService = lobbyService;
            _authService  = authService;
            _userProfile  = userProfile;
            _startupQueue = startupQueue;
            _inputContext = inputContext;

            _lobbyService.OnRoomUpdated      += HandleRoomUpdated;
            _lobbyService.OnGameSessionReady += HandleGameSessionReady;
        }

        // ── UI 입력 점유 (모달 열림/닫힘 시 View가 호출) ───────────────
        // 모달이 떠 있는 동안 게임플레이(Player) 입력을 끈다. 실제 토글은 IInputContext가 담당.

        /// <summary>모달 열림 — 게임플레이 입력 점유 시작.</summary>
        public void BeginUiCapture() => _inputContext.EnterUi();

        /// <summary>모달 닫힘 — 게임플레이 입력 점유 해제.</summary>
        public void EndUiCapture() => _inputContext.ExitUi();

        private void HandleRoomUpdated(RoomInfo room) =>
            Dispatch(new LobbyResult.RoomUpdated(room));

        private void HandleGameSessionReady(string ip, int port, long roomId)
        {
            _isProcessing = false;
            _navigateToGame.OnNext((ip, port));
        }

        public void Initialize()
        {
            // 방 목록 로드는 로비 뷰가 실제로 열릴 때 트리거된다.
            // (LobbyViewController.OpenLobbyAsync 에서 Accept(LoadRooms) 호출)
        }

        /// <summary>
        /// 인증 완료 후 StartupIntentQueue를 소진한다.
        /// Initialize()는 인증 전에 실행될 수 있으므로, 큐 소진은 여기서 처리한다.
        /// </summary>
        public async UniTask StartAsync(CancellationToken ct)
        {
            await _authService.AuthenticatedAsync().AttachExternalCancellation(ct);

            // 방 안에서 "내가 방장인가 / 내가 준비했는가"를 판정하려면 내 public_id 가 State 에 있어야 한다.
            // Reducer 는 순수 함수라 프로필을 직접 읽을 수 없으므로 Result 로 흘려 넣는다.
            Dispatch(new LobbyResult.IdentityResolved(_userProfile.PublicId));

            Debug.Log($"[LobbyModel] StartAsync — auth 완료, StartupIntentQueue HasPending={_startupQueue.HasPending}");

            while (_startupQueue.TryDequeue(out var startupIntent))
            {
                if (startupIntent is RestoreRoomStartupIntent restoreIntent)
                {
                    Debug.Log($"[LobbyModel] StartupIntentQueue: RestoreRoom roomId={restoreIntent.RoomId}");
                    Accept(new LobbyIntent.RestoreRoom(restoreIntent.RoomId));
                }
            }
        }

        // ── View의 단일 진입점 ────────────────────────

        public void Accept(LobbyIntent intent)
        {
            Debug.Log($"[LobbyModel] Accept: {intent.GetType().Name} (isProcessing={_isProcessing})");

            // SelectRoom은 동기 처리 — _isProcessing 무관하게 항상 반응
            if (intent is LobbyIntent.SelectRoom selectRoom)
            {
                HandleSelectRoom(selectRoom);
                return;
            }

            if (_isProcessing)
            {
                Debug.LogWarning($"[LobbyModel] {intent.GetType().Name} 무시됨 — 처리 중");
                return;
            }

            switch (intent)
            {
                case LobbyIntent.LoadRooms _:
                    LoadRoomsAsync().Forget();
                    break;

                case LobbyIntent.CreateRoom createRoom:
                    CreateRoomAsync(createRoom).Forget();
                    break;

                case LobbyIntent.JoinRoom joinRoom:
                    JoinRoomAsync(joinRoom).Forget();
                    break;

                case LobbyIntent.StartGame _:
                    StartGameAsync().Forget();
                    break;

                case LobbyIntent.SetReady setReady:
                    SetReadyAsync(setReady).Forget();
                    break;

                case LobbyIntent.LeaveRoom _:
                    LeaveRoomAsync().Forget();
                    break;

                case LobbyIntent.RestoreRoom restoreRoom:
                    RestoreRoomAsync(restoreRoom).Forget();
                    break;
            }
        }

        private void HandleSelectRoom(LobbyIntent.SelectRoom intent)
        {
            var rooms = _state.Value.Rooms;
            for (var i = 0; i < rooms.Count; i++)
            {
                if (rooms[i].Info.RoomId != intent.RoomId) continue;
                Dispatch(new LobbyResult.RoomSelected(rooms[i]));
                return;
            }
        }

        // ── Effects ───────────────────────────────────

        private async UniTaskVoid LoadRoomsAsync()
        {
            _isProcessing = true;
            Dispatch(LobbyResult.Loading.Instance);
            try
            {
                // 9.6: 첫 페이지만 받는다(서버가 크기 상한·최신순 정렬을 강제). 페이저 UI 는 아직 없어
                // offset 은 항상 0 — 목록이 서버 페이지 크기를 넘으면 그때 UI 를 붙인다(YAGNI).
                var res = await _repository.GetRoomsAsync(ct: _cts.Token);
                Dispatch(res.IsSuccess
                    ? (LobbyResult)new LobbyResult.RoomsLoaded(res.Rooms)
                    : new LobbyResult.Failed(res.Error));
            }
            finally { _isProcessing = false; }
        }

        private async UniTaskVoid CreateRoomAsync(LobbyIntent.CreateRoom intent)
        {
            _isProcessing = true;
            Dispatch(LobbyResult.Loading.Instance);
            Debug.Log($"[LobbyModel] 방 생성 요청 name={intent.Name} maxPlayers={intent.MaxPlayers} mapId={intent.MapId}");
            try
            {
                var res = await _repository.CreateRoomAsync(intent.Name, intent.MaxPlayers, intent.MapId, _cts.Token);
                if (!res.IsSuccess)
                {
                    Debug.LogWarning($"[LobbyModel] 방 생성 실패: {res.Error}");
                    Dispatch(new LobbyResult.Failed(res.Error));
                    return;
                }

                Dispatch(new LobbyResult.RoomCreated(res.Room));
                Debug.Log($"[LobbyModel] 방 생성 완료 — NavigateToRoom 발행 roomId={res.Room.RoomId}");
                _navigateToRoom.OnNext(res.Room.RoomId);
            }
            finally { _isProcessing = false; }
        }

        private async UniTaskVoid JoinRoomAsync(LobbyIntent.JoinRoom intent)
        {
            _isProcessing = true;
            Dispatch(LobbyResult.Loading.Instance);
            Debug.Log($"[LobbyModel] 방 입장 요청 roomId={intent.RoomId}");
            try
            {
                var res = await _repository.JoinRoomAsync(intent.RoomId, _cts.Token);
                if (!res.IsSuccess)
                {
                    Debug.LogWarning($"[LobbyModel] 방 입장 실패: {res.Error}");
                    Dispatch(new LobbyResult.Failed(res.Error));
                    return;
                }

                Dispatch(new LobbyResult.RoomJoined(res.Room));
                Debug.Log($"[LobbyModel] 방 입장 완료 — NavigateToRoom 발행 roomId={intent.RoomId}");
                _navigateToRoom.OnNext(intent.RoomId);
            }
            finally { _isProcessing = false; }
        }

        private async UniTaskVoid StartGameAsync()
        {
            _isProcessing = true;
            Dispatch(LobbyResult.Loading.Instance);
            var res = await _repository.StartGameAsync(_cts.Token);
            if (!res.IsSuccess)
            {
                _isProcessing = false;
                Dispatch(new LobbyResult.Failed(res.Error));
            }
            // 성공: _isProcessing은 HandleGameSessionReady 또는 LeaveRoom에서 해제
        }

        private async UniTaskVoid SetReadyAsync(LobbyIntent.SetReady intent)
        {
            _isProcessing = true;
            try
            {
                // Loading 을 띄우지 않는다 — 토글은 즉발이어야 하고, 갱신은 RoomUpdated 로 되돌아온다.
                var res = await _repository.SetReadyAsync(intent.IsReady, _cts.Token);
                if (!res.IsSuccess)
                {
                    Debug.LogWarning($"[LobbyModel] 준비 상태 변경 실패: {res.Error}");
                    Dispatch(new LobbyResult.Failed(res.Error));
                }
            }
            finally { _isProcessing = false; }
        }

        private async UniTaskVoid RestoreRoomAsync(LobbyIntent.RestoreRoom intent)
        {
            _isProcessing = true;
            Dispatch(LobbyResult.Loading.Instance);
            Debug.Log($"[LobbyModel] 방 복원 요청 roomId={intent.RoomId}");
            try
            {
                var res = await _repository.RestoreRoomAsync(intent.RoomId, _cts.Token);
                if (!res.IsSuccess)
                {
                    Debug.LogWarning($"[LobbyModel] 방 복원 실패: {res.Error}");
                    Dispatch(new LobbyResult.Failed(res.Error));
                    return;
                }

                // 방이 이미 Closed 상태이면 RoomDetail을 열지 않는다.
                if (res.Room.Status == RoomStatusType.Closed)
                {
                    Debug.LogWarning($"[LobbyModel] RestoreRoom: 방이 이미 종료됨 roomId={intent.RoomId}");
                    Dispatch(new LobbyResult.Failed("방이 이미 종료됐습니다."));
                    return;
                }

                Dispatch(new LobbyResult.RoomJoined(res.Room));
                Debug.Log($"[LobbyModel] 방 복원 완료 — NavigateToRoom 발행 " +
                          $"roomId={intent.RoomId}, roomName={res.Room.RoomName}, roomMaxPlayers={res.Room.MaxPlayers}, roomStatus={res.Room.Status}");
                _navigateToRoom.OnNext(intent.RoomId);
            }
            finally { _isProcessing = false; }
        }

        private async UniTaskVoid LeaveRoomAsync()
        {
            _isProcessing = true;
            Dispatch(LobbyResult.Loading.Instance);
            try
            {
                var res = await _repository.LeaveRoomAsync(_cts.Token);
                Dispatch(res.IsSuccess
                    ? (LobbyResult)LobbyResult.RoomLeft.Instance
                    : new LobbyResult.Failed(res.Error));
            }
            finally { _isProcessing = false; }
        }

        // ── Result → Reducer → State ──────────────────

        private void Dispatch(LobbyResult result)
        {
            _state.Value = LobbyReducer.Reduce(_state.Value, result);
        }

        public void Dispose()
        {
            _lobbyService.OnRoomUpdated      -= HandleRoomUpdated;
            _lobbyService.OnGameSessionReady -= HandleGameSessionReady;
            _cts.Cancel();
            _cts.Dispose();
            _state.Dispose();
            _navigateToRoom.Dispose();
            _navigateToGame.Dispose();
        }
    }
}
