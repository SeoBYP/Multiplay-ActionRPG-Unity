using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.OutGame.DungeonLobby;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.GUI.OutGame.Lobby
{
    /// <summary>
    /// 방 생성/입장 후 표시되는 대기실 View.
    ///
    /// MVI 규칙:
    ///   - State를 받아 UI를 렌더링한다.
    ///   - 버튼 입력을 Intent로 변환해 Model에 전달한다.
    /// </summary>
    public class DungeonRoomDetailView : MonoBehaviour
    {
        private const int MinPlayersToStart = 1;

        [Inject] private LobbyModel _model;

        [Header("Header")]
        [SerializeField] private Button m_backButton;

        [Header("Dungeon Room Info")]
        [SerializeField] private TextMeshProUGUI m_roomName;
        [SerializeField] private TextMeshProUGUI m_roomPlayerCurrentCount;
        [SerializeField] private TextMeshProUGUI m_roomPlayerMaxCount;
        [SerializeField] private Button          m_playButton;

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
            // m_playerCharacterSlotParent 아래 기존 슬롯을 풀로 수거
            foreach (Transform child in m_playerCharacterSlotParent)
            {
                var existing = child.GetComponent<DungeonRoomPlayerCharacterSlot>();
                if (existing != null)
                {
                    existing.gameObject.SetActive(false);
                    _freePool.Enqueue(existing);
                }
            }
            _model.State
                .Subscribe(Render)
                .AddTo(destroyCancellationToken);

            m_playButton.onClick.AddListener(() =>
                _model.Accept(LobbyIntent.StartGame.Instance));

            m_backButton.onClick.AddListener(() =>
                _model.Accept(LobbyIntent.LeaveRoom.Instance));
        }

        private void Render(LobbyState state)
        {
            var room = state.SelectedRoom;
            if (room == null) return;

            m_roomName.text               = room.RoomName;
            m_roomPlayerCurrentCount.text = room.PlayerCount.ToString();
            m_roomPlayerMaxCount.text     = room.MaxPlayers.ToString();

            SyncSlots(room.Players);

            // 방이 대기 중이고 최소 인원 이상일 때만 시작 버튼 활성화
            m_playButton.interactable =
                // room.Status == RoomStatus.Waiting &&
                room.PlayerCount >= MinPlayersToStart;
        }

        private void SyncSlots(IReadOnlyList<RoomPlayerInfo> players)
        {
            // 떠난 플레이어 슬롯 → 풀로 반환
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

            // 새 플레이어 → 풀 우선, 없으면 Instantiate
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