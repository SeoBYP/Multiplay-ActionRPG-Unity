using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using R3;
using Game.Presentation.DungeonLobby;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VContainer;

namespace Game.GUI.OutGame.Lobby
{
    /// <summary>
    /// 던전 로비 목록 화면 View.
    ///
    /// MVI 규칙:
    ///   - State를 받아 UI를 렌더링한다.
    ///   - 사용자 입력을 Intent로 변환해 Model에 전달한다.
    ///   - Service / Repository 직접 호출 없음.
    /// </summary>
    public sealed class DungeonRoomLobbyView : MonoBehaviour
    {
        [Inject] private LobbyModel _model;

        [Header("방 목록")]
        [SerializeField] private Transform          roomListParent;
        [SerializeField] private DungeonRoomItemView dungeonRoomItemPrefab;

        [Header("선택된 방 상세")]
        [SerializeField] private GameObject        detailPanel;
        [SerializeField] private TextMeshProUGUI   selectedRoomName;
        [SerializeField] private TextMeshProUGUI   selectedRoomPlayers;
        [SerializeField] private TextMeshProUGUI   selectedRoomStatus;
        [SerializeField] private Button            joinRoomButton;

        [Header("버튼")]
        [SerializeField] private Button refreshButton;
        [SerializeField] private Button createRoomButton;

        private readonly Dictionary<long, DungeonRoomItemView> _itemMap
            = new Dictionary<long, DungeonRoomItemView>();

        /// <summary>씬에 미리 배치된 DungeonRoomItemView 를 재사용하기 위한 풀.</summary>
        private readonly Queue<DungeonRoomItemView> _freePool = new Queue<DungeonRoomItemView>();

        private AddressableInstance? _popupInst;

        private void Start()
        {
            // roomListParent 아래에 미리 배치된 DungeonRoomItemView 를 풀로 수거
            foreach (Transform child in roomListParent)
            {
                var existing = child.GetComponent<DungeonRoomItemView>();
                if (existing != null)
                {
                    existing.gameObject.SetActive(false);
                    _freePool.Enqueue(existing);
                }
            }
            Debug.Log($"[DungeonRoomLobbyView] Start — 풀에 수거된 아이템: {_freePool.Count}개");

            _model.State
                .Subscribe(Render)
                .AddTo(destroyCancellationToken);

            refreshButton.onClick.AddListener(() =>
                _model.Accept(LobbyIntent.LoadRooms.Instance));

            createRoomButton.onClick.AddListener(() =>
                OpenCreateRoomPopupAsync().Forget());

            joinRoomButton.onClick.AddListener(OnJoinClicked);

            // 이 오브젝트가 활성인 동안 게임플레이 입력 점유. btn_close는 이 GameObject를
            // SetActive(false)로 끄므로 → OnDisable에서 자동 해제(버튼 리스너에 의존하지 않음).
            gameObject.AddComponent<UiInputCaptureBehaviour>()
                      .Bind(_model.BeginUiCapture, _model.EndUiCapture);

            detailPanel.SetActive(false);
        }

        // ── Render: State → UI ───────────────────────

        private void Render(LobbyState state)
        {
            Debug.Log($"[DungeonRoomLobbyView] Render — IsLoading={state.IsLoading} IsInRoom={state.IsInRoom} Rooms={state.Rooms?.Count ?? 0}개 Error={state.ErrorMessage ?? "없음"}");

            if (!state.IsLoading)
            {
                SyncRoomList(state.Rooms);
                RefreshSelectedHighlight(state.SelectedRoom?.RoomId);
            }

            RenderSelectedRoom(state.SelectedRoom);
        }

        private void RenderSelectedRoom(DungeonRoomModel room)
        {
            detailPanel.SetActive(room != null);
            if (room == null) return;

            selectedRoomName.text    = room.RoomName;
            selectedRoomPlayers.text = $"{room.PlayerCount} / {room.MaxPlayers}";
            selectedRoomStatus.text  = ToStatusText(room.Status);

            var canJoin = room.Status == RoomStatus.Waiting
                       && room.PlayerCount < room.MaxPlayers;
            joinRoomButton.interactable = canJoin;
        }

        /// <summary>State.SelectedRoom 기반으로 각 아이템의 선택 하이라이트를 갱신한다.</summary>
        private void RefreshSelectedHighlight(long? selectedRoomId)
        {
            foreach (var kv in _itemMap)
                kv.Value.SetSelected(kv.Key == selectedRoomId);
        }

        private void OnJoinClicked()
        {
            var selected = _model.State.CurrentValue.SelectedRoom;
            if (selected == null)
            {
                Debug.LogWarning("[DungeonRoomLobbyView] JoinRoom 클릭 — 선택된 방 없음");
                return;
            }
            Debug.Log($"[DungeonRoomLobbyView] JoinRoom 클릭 — roomId={selected.RoomId} roomName={selected.RoomName}");
            _model.Accept(new LobbyIntent.JoinRoom(selected.RoomId));
        }

        // ── CreateRoomPopup ─────────────────────────

        private async UniTaskVoid OpenCreateRoomPopupAsync()
        {
            if (_popupInst != null) return;

            _popupInst = await AddressableLoader.LoadAndInstantiateAsync(
                AddressKeys.UI.CreateRoomPopup, transform.root, destroyCancellationToken);

            if (_popupInst == null) return; // 로드 실패/취소

            // 모달이 열려 있는 동안 게임플레이 입력 점유(닫힘 Dispose에 해제 자동 연결)
            _popupInst.CaptureWhileOpen(_model);
            _popupInst.GameObject.GetComponent<CreateDungeonRoomPopupView>().Setup(_model, ClosePopup);
        }

        private void ClosePopup()
        {
            _popupInst?.Dispose(); // Dispose가 점유 해제까지 처리
            _popupInst = null;
        }

        // ── 방 목록 Diff 갱신 ───────────────────────

        private void SyncRoomList(IReadOnlyList<DungeonRoomModel> rooms)
        {
            Debug.Log($"[DungeonRoomLobbyView] SyncRoomList — 방 {rooms?.Count ?? 0}개");

            var nextIds = new HashSet<long>();
            foreach (var room in rooms) nextIds.Add(room.RoomId);

            // 사라진 방 → 풀로 반환 (Destroy 대신 재사용)
            var toRemove = new List<long>();
            foreach (var id in _itemMap.Keys)
                if (!nextIds.Contains(id)) toRemove.Add(id);

            foreach (var id in toRemove)
            {
                var item = _itemMap[id];
                item.gameObject.SetActive(false);
                _freePool.Enqueue(item);
                _itemMap.Remove(id);
            }

            // 새 방 → 풀 우선 사용, 없으면 Instantiate
            foreach (var room in rooms)
            {
                if (!_itemMap.TryGetValue(room.RoomId, out var item))
                {
                    if (_freePool.Count > 0)
                    {
                        item = _freePool.Dequeue();
                        Debug.Log($"[DungeonRoomLobbyView] 풀에서 아이템 재사용 roomId={room.RoomId}");
                    }
                    else
                    {
                        item = Instantiate(dungeonRoomItemPrefab, roomListParent);
                        Debug.Log($"[DungeonRoomLobbyView] 새 아이템 Instantiate roomId={room.RoomId}");
                    }
                    item.gameObject.SetActive(true);
                    _itemMap[room.RoomId] = item;
                }
                item.Setup(room, _model);
                Debug.Log($"[DungeonRoomLobbyView]   └ roomId={room.RoomId} name={room.RoomName} ({room.PlayerCount}/{room.MaxPlayers}) {room.Status}");
            }
        }

        // ── 파생값 계산 (View 책임) ───────────────────

        private static string ToStatusText(RoomStatus status)
        {
            switch (status)
            {
                case RoomStatus.Waiting:  return "대기 중";
                case RoomStatus.Starting: return "시작 중";
                case RoomStatus.Playing:  return "게임 중";
                case RoomStatus.Closed:   return "닫힘";
                default:                  return "-";
            }
        }

        private void OnDestroy()
        {
            ClosePopup();
        }
    }
}
