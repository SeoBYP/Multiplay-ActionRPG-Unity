using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using VContainer.Unity;

namespace Game.OutGame.DungeonLobby
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
    public sealed class LobbyModel : IInitializable, IDisposable
    {
        private readonly LobbyRepository _repository;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private readonly ReactiveProperty<LobbyState> _state
            = new ReactiveProperty<LobbyState>(LobbyState.Initial);

        private readonly Subject<long> _navigateToRoom = new Subject<long>();

        private bool _isProcessing;

        public ReadOnlyReactiveProperty<LobbyState> State =>
            _state.ToReadOnlyReactiveProperty();

        /// <summary>입장 성공 시 발행 — Router가 구독해서 화면 전환.</summary>
        public Observable<long> NavigateToRoom => _navigateToRoom;

        public LobbyModel(LobbyRepository repository)
        {
            _repository = repository;
        }

        public void Initialize()
        {
            // 방 목록 로드는 로비 뷰가 실제로 열릴 때 트리거된다.
            // (LobbyViewController.OpenLobbyAsync 에서 Accept(LoadRooms) 호출)
        }

        // ── View의 단일 진입점 ────────────────────────

        public void Accept(LobbyIntent intent)
        {
            // SelectRoom은 동기 처리 — _isProcessing 무관하게 항상 반응
            if (intent is LobbyIntent.SelectRoom selectRoom)
            {
                HandleSelectRoom(selectRoom);
                return;
            }

            if (_isProcessing) return;

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
                var res = await _repository.GetRoomsAsync(_cts.Token);
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
            try
            {
                var res = await _repository.CreateRoomAsync(intent.Name, intent.MaxPlayers, _cts.Token);
                if (!res.IsSuccess) { Dispatch(new LobbyResult.Failed(res.Error)); return; }

                Dispatch(new LobbyResult.RoomCreated(res.Room));
                _navigateToRoom.OnNext(res.Room.RoomId);
            }
            finally { _isProcessing = false; }
        }

        private async UniTaskVoid JoinRoomAsync(LobbyIntent.JoinRoom intent)
        {
            _isProcessing = true;
            Dispatch(LobbyResult.Loading.Instance);
            try
            {
                var res = await _repository.JoinRoomAsync(intent.RoomId, _cts.Token);
                if (!res.IsSuccess) { Dispatch(new LobbyResult.Failed(res.Error)); return; }

                Dispatch(new LobbyResult.RoomJoined(res.Room));
                _navigateToRoom.OnNext(intent.RoomId);
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
            _cts.Cancel();
            _cts.Dispose();
            _state.Dispose();
            _navigateToRoom.Dispose();
        }
    }
}
