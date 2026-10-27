using Game.Presentation.DungeonLobby;
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
            _roomId = room.RoomId;

            roomNameText.text    = room.RoomName;
            playerCountText.text = $"{room.PlayerCount}/{room.MaxPlayers}";
            statusText.text      = ToStatusText(room.Status);

            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(OnSelectClicked);
        }

        /// <summary>선택 여부는 LobbyView가 State 기반으로 갱신한다 — Item이 직접 판단하지 않는다.</summary>
        public void SetSelected(bool isSelected)
        {
            if (selected != null)
                selected.SetActive(isSelected);
        }

        private void OnSelectClicked()
        {
            _model.Accept(new LobbyIntent.SelectRoom(_roomId));
        }

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
    }
}
