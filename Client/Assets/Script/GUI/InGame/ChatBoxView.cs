using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.GUI.InGame
{
    public class ChatBoxView : MonoBehaviour
    {
        [SerializeField] private InputField _inputField;
        [SerializeField] private Dropdown _chatDropdown;
        [SerializeField] private Button _sendButton;
        [SerializeField] private GameObject chatBubblePrefab;
        [SerializeField] private int ChatBubblePoolSize;
        [SerializeField] private ScrollRect _scrollRect;
        [SerializeField] private RectTransform _chatContentParent;
    }
}
