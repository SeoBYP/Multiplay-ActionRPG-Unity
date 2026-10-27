using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.GUI.Common;
using Game.Presentation.DungeonLobby;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.GUI.OutGame.Lobby
{
    /// <summary>
    /// 방 생성/입장 후 표시되는 대기실 View(MVI). State 렌더 + 버튼 입력을 Intent 로 Model 에 전달.
    /// </summary>
    public class DungeonRoomDetailView : MonoBehaviour
    {
        private const int MinPlayersToStart = 1;

        [Inject] private LobbyModel _model;

        // 버튼 하나가 방장/비방장에서 뜻이 달라지므로, 마지막 Render 의 판정을 클릭에서 재사용한다.
        private bool _isHost;
        private bool _isReady;

        [Header("Header")]
        [SerializeField] private Button m_backButton;

        [Header("Dungeon Room Info")]
        [SerializeField] private TextMeshProUGUI m_roomName;
        [SerializeField] private TextMeshProUGUI m_roomPlayerCurrentCount;
        [SerializeField] private TextMeshProUGUI m_roomPlayerMaxCount;

        /// <summary>
        /// 역할에 따라 뜻이 바뀌는 단일 버튼.
        ///   방장   → "시작"(비방장 전원 준비 시에만 활성)
        ///   비방장 → "준비" / "준비 해제"
        /// 버튼을 둘로 나누지 않은 이유: 한 사람에게 동시에 보일 일이 없어 프리팹만 복잡해진다.
        /// </summary>
        [SerializeField] private Button          m_playButton;

        /// <summary>버튼 라벨. 미배선이면 Start()에서 자식 텍스트를 찾아 쓴다.</summary>
        [SerializeField] private TextMeshProUGUI m_playButtonLabel;

        [Header("Player Character Slot")]
        [SerializeField] private Transform                    m_playerCharacterSlotParent;
        [SerializeField] private DungeonRoomPlayerCharacterSlot m_playerCharacterSlotPrefab;

        // PublicId → 슬롯 인스턴스
        private readonly Dictionary<string, DungeonRoomPlayerCharacterSlot> _slotMap
            = new Dictionary<string, DungeonRoomPlayerCharacterSlot>();

        /// <summary>씬에 미리 배치된 슬롯을 재사용하기 위한 풀.</summary>
        private readonly Queue<DungeonRoomPlayerCharacterSlot> _freePool
            = new Queue<DungeonRoomPlayerCharacterSlot>();

        private void Start()
        {
            foreach (Transform child in m_playerCharacterSlotParent)
            {
                var existing = child.GetComponent<DungeonRoomPlayerCharacterSlot>();
                if (existing != null)
                {
                    existing.gameObject.SetActive(false);
                    _freePool.Enqueue(existing);
                }
            }
            // 구독보다 먼저 배선한다 — Subscribe 즉시 Render 가 동기로 돌므로, 라벨을 나중에 찾으면 첫 Render 가 라벨을 못 쓴다.
            ResolvePlayButtonLabel();

            m_playButton.onClick.AddListener(OnPlayButtonClicked);

            _model.State
                .Subscribe(Render)
                .AddTo(destroyCancellationToken);

            m_backButton.onClick.AddListener(() => ShowLeaveConfirmAsync().Forget());
        }

        /// <summary>버튼이 두 뜻을 가지므로 Render 에서 캐시한 State 로 Intent 를 정한다(Model 을 다시 읽지 않는다).</summary>
        private void OnPlayButtonClicked()
        {
            if (_isHost)
            {
                _model.Accept(LobbyIntent.StartGame.Instance);
                return;
            }

            _model.Accept(new LobbyIntent.SetReady(!_isReady));
        }

        private void Render(LobbyState state)
        {
            var room = state.SelectedRoom;
            if (room == null) return;

            m_roomName.text               = room.RoomName;
            m_roomPlayerCurrentCount.text = room.PlayerCount.ToString();
            m_roomPlayerMaxCount.text     = room.MaxPlayers.ToString();

            SyncSlots(room.Players);

            _isHost  = room.IsHost(state.MyPublicId);
            _isReady = room.IsReady(state.MyPublicId);

            var isWaiting = room.Status == RoomStatus.Waiting;

            if (_isHost)
            {
                // 방장: 비방장 전원이 준비해야 시작할 수 있다. (최종 판정은 서버 — 여기서는 UX 만)
                SetPlayButtonLabel("시작");
                m_playButton.interactable =
                    isWaiting &&
                    room.PlayerCount >= MinPlayersToStart &&
                    room.AllOthersReady;
            }
            else
            {
                SetPlayButtonLabel(_isReady ? "준비 해제" : "준비");
                m_playButton.interactable = isWaiting;
            }
        }

        private void ResolvePlayButtonLabel()
        {
            if (m_playButtonLabel == null)
                m_playButtonLabel = m_playButton.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        private void SetPlayButtonLabel(string label)
        {
            // 배선 순서에 기대지 않는다 — 못 찾았으면 이 자리에서 다시 찾는다.
            ResolvePlayButtonLabel();
            if (m_playButtonLabel != null)
                m_playButtonLabel.text = label;
        }

        private async UniTaskVoid ShowLeaveConfirmAsync()
        {
            var inst = await AddressableLoader.LoadAndInstantiateAsync(
                AddressKeys.UI.ConfirmPopup, transform.root, destroyCancellationToken);
            if (inst == null) return;

            var popup = inst.GameObject.GetComponent<ConfirmPopup>();
            popup.SetAddressableOwner(inst);
            popup.Setup("방 나가기", "정말 방을 나가시겠습니까?",
                onConfirm: () => _model.Accept(LobbyIntent.LeaveRoom.Instance));
        }

        private void SyncSlots(IReadOnlyList<RoomPlayerInfo> players)
        {
            var nextIds = new HashSet<string>();
            foreach (var p in players) nextIds.Add(p.PublicId);

            var toRemove = new List<string>();
            foreach (var id in _slotMap.Keys)
                if (!nextIds.Contains(id)) toRemove.Add(id);

            foreach (var id in toRemove)
            {
                var slot = _slotMap[id];
                slot.gameObject.SetActive(false);
                _freePool.Enqueue(slot);
                _slotMap.Remove(id);
            }

            foreach (var player in players)
            {
                if (!_slotMap.TryGetValue(player.PublicId, out var slot))
                {
                    slot = _freePool.Count > 0
                        ? _freePool.Dequeue()
                        : Instantiate(m_playerCharacterSlotPrefab, m_playerCharacterSlotParent);

                    slot.gameObject.SetActive(true);
                    _slotMap[player.PublicId] = slot;
                }
                slot.Setup(player);
            }
        }
    }
}