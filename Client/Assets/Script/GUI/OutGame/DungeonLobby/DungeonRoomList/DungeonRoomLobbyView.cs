using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using R3;
using Game.OutGame.DungeonLobby;
using GameServer.Grpc.DungeonLobby;
using Script.System.Auth;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
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
        [Inject] private LobbyModel  _model;
        [Inject] private UserProfile _userProfile;

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

        private AsyncOperationHandle<GameObject> _popupHandle;
        private GameObject                       _popupInstance;

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
            Debug.Log($"[DungeonRoomLobbyView] Start — 유저: PublicId={_userProfile.PublicId} NickName={_userProfile.NickName} | 풀에 수거된 아이템: {_freePool.Count}개");

            _model.State
                .Subscribe(Render)
                .AddTo(destroyCancellationToken);

            refreshButton.onClick.AddListener(() =>
                _model.Accept(LobbyIntent.LoadRooms.Instance));

            createRoomButton.onClick.AddListener(() =>
                OpenCreateRoomPopupAsync().Forget());

            joinRoomButton.onClick.AddListener(OnJoinClicked);

            detailPanel.SetActive(false);
        }

        // ── Render: State → UI ───────────────────────

        private void Render(LobbyState state)
        {
            Debug.Log($"[DungeonRoomLobbyView] Render — IsLoading={state.IsLoading} IsInRoom={state.IsInRoom} Rooms={state.Rooms?.Count ?? 0}개 Error={state.ErrorMessage ?? "없음"} | 유저={_userProfile.PublicId}");

            if (!state.IsLoading)
            {
                SyncRoomList(state.Rooms);
                RefreshSelectedHighlight(state.SelectedRoom?.Info.RoomId);
            }

            RenderSelectedRoom(state.SelectedRoom);
        }

        private void RenderSelectedRoom(DungeonRoomModel room)
        {
            detailPanel.SetActive(room != null);
            if (room == null) return;

            selectedRoomName.text    = room.Info.RoomName;
            selectedRoomPlayers.text = $"{room.Info.CurrentPlayers.Count} / {room.Info.MaxPlayers}";
            selectedRoomStatus.text  = ToStatusText(room.Info.Status);

            var canJoin = room.Info.Status == RoomStatusType.Waiting
                       && room.Info.CurrentPlayers.Count < room.Info.MaxPlayers;
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
                Debug.LogWarning($"[DungeonRoomLobbyView] JoinRoom 클릭 — 선택된 방 없음 | 유저={_userProfile.PublicId}");
                return;
            }
            Debug.Log($"[DungeonRoomLobbyView] JoinRoom 클릭 — roomId={selected.Info.RoomId} roomName={selected.Info.RoomName} | 유저={_userProfile.PublicId}");
            _model.Accept(new LobbyIntent.JoinRoom(selected.Info.RoomId));
        }

        // ── CreateRoomPopup ─────────────────────────

        private async UniTaskVoid OpenCreateRoomPopupAsync()
        {
            if (_popupInstance != null) return;

            _popupHandle   = Addressables.LoadAssetAsync<GameObject>(AddressKeys.UI.CreateRoomPopup);
            var prefab     = await _popupHandle.Task;
            _popupInstance = Instantiate(prefab, transform.root);
            _popupInstance.GetComponent<CreateDungeonRoomPopupView>().Setup(_model, ClosePopup);
        }

        private void ClosePopup()
        {
            if (_popupInstance != null)
            {
                Destroy(_popupInstance);
                _popupInstance = null;
            }
            if (_popupHandle.IsValid())
                Addressables.Release(_popupHandle);
        }

        // ── 방 목록 Diff 갱신 ───────────────────────

        private void SyncRoomList(IReadOnlyList<DungeonRoomModel> rooms)
        {
            Debug.Log($"[DungeonRoomLobbyView] SyncRoomList — 방 {rooms?.Count ?? 0}개 | 유저={_userProfile.PublicId}");

            var nextIds = new HashSet<long>();
            foreach (var room in rooms) nextIds.Add(room.Info.RoomId);

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
                if (!_itemMap.TryGetValue(room.Info.RoomId, out var item))
                {
                    if (_freePool.Count > 0)
                    {
                        item = _freePool.Dequeue();
                        Debug.Log($"[DungeonRoomLobbyView] 풀에서 아이템 재사용 roomId={room.Info.RoomId}");
                    }
                    else
                    {
                        item = Instantiate(dungeonRoomItemPrefab, roomListParent);
                        Debug.Log($"[DungeonRoomLobbyView] 새 아이템 Instantiate roomId={room.Info.RoomId}");
                    }
                    item.gameObject.SetActive(true);
                    _itemMap[room.Info.RoomId] = item;
                }
                item.Setup(room, _model);
                Debug.Log($"[DungeonRoomLobbyView]   └ roomId={room.Info.RoomId} name={room.Info.RoomName} ({room.Info.CurrentPlayers.Count}/{room.Info.MaxPlayers}) {room.Info.Status}");
            }
        }

        // ── 파생값 계산 (View 책임) ───────────────────

        private static string ToStatusText(RoomStatusType status)
        {
            switch (status)
            {
                case RoomStatusType.Waiting:  return "대기 중";
                case RoomStatusType.Starting: return "시작 중";
                case RoomStatusType.Playing:  return "게임 중";
                case RoomStatusType.Closed:   return "닫힘";
                default:                      return "-";
            }
        }

        private void OnDestroy()
        {
            ClosePopup();
        }
    }
}
