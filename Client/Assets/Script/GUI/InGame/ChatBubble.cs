using UnityEngine;
using TMPro;

namespace Game.GUI.InGame
{
    public class ChatBubble : MonoBehaviour
    {
        [SerializeField] private TMP_Text sender;
        [SerializeField] private TMP_Text message;
    }
}
