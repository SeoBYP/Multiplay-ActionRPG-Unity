using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.GUI;
using Game.GUI.Common;
using Game.Gameplay.Input;
using Game.Presentation.DungeonLobby;
using R3;
using UnityEngine;
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
    /// auth 대기 및 StartupIntentQueue 소진은 LobbyModel.StartAsync 책임.
    /// IInputHandler.Priority = 100 (UI는 월드 인터랙션보다 우선)
    /// L키를 이 핸들러가 소비하면 하위 핸들러는 해당 프레임 L키를 받지 못한다.
    /// </summary>
    public sealed class LobbyViewController : IInputHandler, IInitializable, IDisposable
    {
        private readonly LobbyModel         _model;
        private readonly IObjectResolver    _resolver;
        private readonly InputRouter        _router;

        public int Priority => 100;

        private AddressableInstance?     _lobbyInst;
        private AddressableInstance?     _detailInst;
        private CancellationTokenSource  _cts;

        public LobbyViewController(
            LobbyModel      model,
            IObjectResolver resolver,
            InputRouter     router)
        {
            _model    = model;
            _resolver = resolver;
            _router   = router;
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
            var prevError = (string)null;
            _model.State
                .Subscribe(s =>
                {
                    if (wasInRoom && !s.IsInRoom)
                        CloseRoomDetail();
                    wasInRoom = s.IsInRoom;

                    if (s.ErrorMessage != null && s.ErrorMessage != prevError)
                        ShowErrorPopupAsync(s.ErrorMessage).Forget();
                    prevError = s.ErrorMessage;
                })
                .AddTo(_cts.Token);

            // SocketServer 준비 완료 — RoomDetail은 그대로 유지한다.
            // 던전 씬이 로드되면 씬 전환이 자연스럽게 정리하므로 여기서 닫지 않는다.
            // (NavigateToGame 시점에 CloseRoomDetail 하면 Addressable 로드 중일 때 handle이 무효화됨)
            _model.NavigateToGame
                .Subscribe(args =>
                {
                    Debug.Log($"[LobbyViewController] 게임 세션 준비 완료 — {args.Ip}:{args.Port}");
                })
                .AddTo(_cts.Token);

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

            if (_lobbyInst != null) CloseLobby();
            else                    OpenLobbyAsync().Forget();

            return true;
        }

        // ── Addressable 로드 / 해제 ──────────────────

        private async UniTaskVoid OpenLobbyAsync()
        {
            if (_lobbyInst != null) return;

            // 로비가 열리는 시점에 방 목록을 로드한다.
            _model.Accept(LobbyIntent.LoadRooms.Instance);

            _lobbyInst = await AddressableLoader.LoadAndInstantiateAsync(
                AddressKeys.UI.LobbyView, GUIRoot.Instance.transform, _cts.Token);

            if (_lobbyInst != null)
                _resolver.InjectGameObject(_lobbyInst.GameObject);
        }

        private void CloseLobby()
        {
            _lobbyInst?.Dispose();
            _lobbyInst = null;
        }

        // ── RoomDetail 로드 / 해제 ───────────────────

        private async UniTaskVoid OpenRoomDetailAsync()
        {
            Debug.Log($"[LobbyViewController] OpenRoomDetailAsync 진입 — detailInst={(_detailInst != null ? "있음" : "null")}");
            if (_detailInst != null) return;

            Debug.Log("[LobbyViewController] RoomDetail 로드 시작");
            _detailInst = await AddressableLoader.LoadAndInstantiateAsync(
                AddressKeys.UI.RoomDetailView, GUIRoot.Instance.transform, _cts.Token);

            if (_detailInst != null)
            {
                _resolver.InjectGameObject(_detailInst.GameObject);
                Debug.Log("[LobbyViewController] RoomDetail 로드 완료");
            }
        }

        private void CloseRoomDetail()
        {
            _detailInst?.Dispose();
            _detailInst = null;
        }

        // ── 에러 팝업 ────────────────────────────────

        private async UniTaskVoid ShowErrorPopupAsync(string error)
        {
            var inst = await AddressableLoader.LoadAndInstantiateAsync(
                AddressKeys.UI.AlertPopup, GUIRoot.Instance.transform, _cts.Token);
            if (inst == null) return;

            var popup = inst.GameObject.GetComponent<AlertPopup>();
            popup.SetAddressableOwner(inst);
            popup.Setup("오류", error, glow: PopupGlowType.Danger);
        }
    }
}
