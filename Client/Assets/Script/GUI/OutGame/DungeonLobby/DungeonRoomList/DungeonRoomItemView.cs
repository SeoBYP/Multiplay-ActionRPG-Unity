using GameServer.Grpc.DungeonLobby;
using Game.OutGame.DungeonLobby;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.GUI.OutGame.Lobby
{
    /// <summary>
    /// 방 목록에서 방 1개를 표현하는 View.
    /// LobbyModel.Accept(Intent)만 호출하며, 상태를 직접 바꾸지 않는다.
    /// </summary>
    public sealed class DungeonRoomItemView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI roomNameText;
        [SerializeField] private TextMeshProUGUI playerCountText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Button          selectButton;
        [SerializeField] private GameObject      selected; // 선택 하이라이트 오브젝트

        private LobbyModel _model;
        private long       _roomId;

        /// <summary>방 데이터와 Model 주입. LobbyView가 호출한다.</summary>
        public void Setup(DungeonRoomModel room, LobbyModel model)
        {
            _model  = model;
            _roomId = room.Info.RoomId;

            roomNameText.text    = room.Info.RoomName;
            playerCountText.text = $"{room.Info.CurrentPlayers.Count}/{room.Info.MaxPlayers}";
            statusText.text      = ToStatusText(room.Info.Status);

            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(OnSelectClicked);
        }

        /// <summary>
        /// 이 방이 선택됐는지 여부를 외부(LobbyView)에서 State 기반으로 갱신한다.
        /// MVI 규칙 — Item이 직접 판단하지 않음.
        /// </summary>
        public void SetSelected(bool isSelected)
        {
            if (selected != null)
                selected.SetActive(isSelected);
        }

        // ── Intent 발행 ──────────────────────────────

        private void OnSelectClicked()
        {
            _model.Accept(new LobbyIntent.SelectRoom(_roomId));
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
    }
}
