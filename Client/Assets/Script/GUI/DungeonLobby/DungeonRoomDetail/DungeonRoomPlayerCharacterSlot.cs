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

        [Header("Ready / Host 표시 (선택 — 미배선이면 이름 옆 뱃지 텍스트로 대체)")]
        [SerializeField] private GameObject m_readyBadge;
        [SerializeField] private GameObject m_hostBadge;

        public void Setup(RoomPlayerInfo player)
        {
            // NickName이 없으면 PublicId로 대체하는 정책은 RoomPlayerInfo 생성자에서 처리됨
            var hasBadgeObjects = m_readyBadge != null || m_hostBadge != null;

            m_characterName.text = hasBadgeObjects
                ? player.NickName
                : BuildLabelWithSuffix(player);

            // 프리팹에 뱃지가 배선돼 있으면 그쪽이 우선. 없으면 위 텍스트 접미사가 같은 정보를 나른다.
            if (m_hostBadge != null)  m_hostBadge.SetActive(player.IsHost);
            if (m_readyBadge != null) m_readyBadge.SetActive(!player.IsHost && player.IsReady);
        }

        private static string BuildLabelWithSuffix(RoomPlayerInfo player)
        {
            if (player.IsHost)  return $"{player.NickName} (방장)";
            if (player.IsReady) return $"{player.NickName} (준비 완료)";
            return player.NickName;
        }
    }
}
