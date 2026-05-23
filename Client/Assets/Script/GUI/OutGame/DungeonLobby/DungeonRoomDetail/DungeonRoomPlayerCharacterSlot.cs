using GameServer.Grpc.User;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.GUI.OutGame
{
    public class DungeonRoomPlayerCharacterSlot : MonoBehaviour
    {
        [SerializeField] private Image           m_characterIcon;
        [SerializeField] private TextMeshProUGUI m_characterName;

        public void Setup(UserInfo user)
        {
            // NickName이 없으면 PublicId로 대체
            m_characterName.text = string.IsNullOrEmpty(user.NickName) ? user.PublicId : user.NickName;
        }
    }
}