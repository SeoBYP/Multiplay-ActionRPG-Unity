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
    /// 로비 화면 생명주기 컨트롤러 (POCO). L키 → LobbyView 로드/표시, NavigateToRoom → 로비 닫고 대기실 열기.
    /// auth 대기·StartupIntentQueue 소진은 LobbyModel.StartAsync 책임.
    /// Priority 100 (UI는 월드 인터랙션보다 우선) — 이 핸들러가 L키를 소비하면 하위 핸들러는 그 프레임 L키를 받지 못한다.
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

        // 로드는 프레임을 넘기므로 같은 요청이 겹치면 인스턴스가 둘 생기고 앞의 것이 미아가 된다
        // (GUIRoot 는 DontDestroyOnLoad 라 씬을 옮겨도 남는다) → 로딩 중 재진입 가드.
        private bool _lobbyLoading;
        private bool _detailLoading;
        private bool _closed;

        // 닫힘 세대. 닫을 때마다 올려 그 전에 시작된 로드를 무효화한다(로드 도중 던전 입장 신호는 Dispose·취소가 아니다).
        private int _lobbyGeneration;
        private int _detailGeneration;

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

            // SocketServer 준비 완료 = 던전 입장 — 로비 계열 창을 명시적으로 닫는다(씬 전환에 맡기지 않는다:
            // 미아 인스턴스는 씬 전환으로도 사라지지 않는다). 로드 도중 닫기는 열기 경로의 in-flight 가드가 처리한다.
            _model.NavigateToGame
                .Subscribe(args =>
                {
                    Debug.Log($"[LobbyViewController] 게임 세션 준비 완료 — {args.Ip}:{args.Port} · 로비/대기실 닫기");
                    CloseLobby();
                    CloseRoomDetail();
                })
                .AddTo(_cts.Token);

        }

        public void Dispose()
        {
            _closed = true;
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

            // L = 열기/표시 전용(닫지 않음): 없음 → 로드 / 숨겨짐 → 다시 표시 / 표시 중 → 목록 갱신.
            // 표시 중엔 입력이 캡처돼 Player 맵이 꺼지므로 L이 들어오지 않는다.
            OpenLobbyAsync().Forget();
            return true;
        }

        // ── Addressable 로드 / 해제 ──────────────────

        private async UniTaskVoid OpenLobbyAsync()
        {
            // 이미 로드돼 있으면(X 버튼이 숨긴 상태 포함) 다시 활성화한다. 닫기(숨김)는 X 버튼 담당.
            if (_lobbyInst != null)
            {
                var hidden = _lobbyInst.GameObject.GetComponentInChildren<Game.GUI.OutGame.Lobby.DungeonRoomLobbyView>(true);
                if (hidden != null) hidden.gameObject.SetActive(true);
                _model.Accept(LobbyIntent.LoadRooms.Instance);
                return;
            }

            if (_lobbyLoading) return;
            _lobbyLoading = true;
            int generation = _lobbyGeneration;

            try
            {
                _model.Accept(LobbyIntent.LoadRooms.Instance);

                var inst = await AddressableLoader.LoadAndInstantiateAsync(
                    AddressKeys.UI.LobbyView, GUIRoot.Instance.transform, _cts.Token);
                if (inst == null) return;

                // 로드 중에 닫혔다면(던전 입장·씬 전환) 여기서 치운다 — 아니면 미아가 된다.
                if (_closed || _cts.IsCancellationRequested || generation != _lobbyGeneration)
                {
                    inst.Dispose();
                    return;
                }

                _lobbyInst = inst;
                _resolver.InjectGameObject(inst.GameObject);
                // 입력 점유는 DungeonRoomLobbyView의 UiInputCaptureBehaviour가 담당(X로 숨기면 OnDisable에서 해제).
            }
            catch (OperationCanceledException)
            {
                // 씬 전환 등으로 취소 — 정상 경로
            }
            finally
            {
                _lobbyLoading = false;
            }
        }

        private void CloseLobby()
        {
            _lobbyGeneration++;    // 진행 중인 로드 무효화
            _lobbyInst?.Dispose(); // Dispose가 점유 해제까지 처리(CaptureWhileOpen)
            _lobbyInst = null;
        }

        // ── RoomDetail 로드 / 해제 ───────────────────

        private async UniTaskVoid OpenRoomDetailAsync()
        {
            Debug.Log($"[LobbyViewController] OpenRoomDetailAsync 진입 — detailInst={(_detailInst != null ? "있음" : "null")}");
            if (_detailInst != null || _detailLoading) return;
            _detailLoading = true;
            int generation = _detailGeneration;

            try
            {
                Debug.Log("[LobbyViewController] RoomDetail 로드 시작");
                var inst = await AddressableLoader.LoadAndInstantiateAsync(
                    AddressKeys.UI.RoomDetailView, GUIRoot.Instance.transform, _cts.Token);
                if (inst == null) return;

                if (_closed || _cts.IsCancellationRequested || generation != _detailGeneration)
                {
                    inst.Dispose(); // 로드 도중 던전 입장/씬 전환 — 미아로 남기지 않는다
                    return;
                }

                _detailInst = inst;
                _resolver.InjectGameObject(inst.GameObject);
                inst.CaptureWhileOpen(_model); // 방 상세 떠 있는 동안 게임플레이 입력 점유(닫힘에 자동 해제)
                Debug.Log("[LobbyViewController] RoomDetail 로드 완료");
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _detailLoading = false;
            }
        }

        private void CloseRoomDetail()
        {
            _detailGeneration++;
            _detailInst?.Dispose(); // Dispose가 점유 해제까지 처리
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
