using UnityEngine;
using TMPro;

namespace Game.GUI.InGame
{
    public class HUD : MonoBehaviour
    {
        [SerializeField] private ChatBoxView chatBox;
        [SerializeField] private TMP_Text playerNickname;
    }
}
