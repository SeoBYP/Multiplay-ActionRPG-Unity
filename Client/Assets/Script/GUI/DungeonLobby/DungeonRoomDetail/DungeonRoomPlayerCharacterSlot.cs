using Game.Presentation.DungeonLobby;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.GUI.OutGame
{
    public class DungeonRoomPlayerCharacterSlot : MonoBehaviour
    {
        [SerializeField] private Image           m_characterIcon;
        [SerializeField] private TextMeshProUGUI m_characterName;

        public void Setup(RoomPlayerInfo player)
        {
            // NickName이 없으면 PublicId로 대체하는 정책은 RoomPlayerInfo 생성자에서 처리됨
            m_characterName.text = player.NickName;
        }
    }
}