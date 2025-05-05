using System;
using System.Collections.Generic;
using Game.Managers;
using Game.Network;
using Script.GUI.Sub;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChatBox : MonoBehaviour
{
    [SerializeField] private TMP_InputField _inputField;
    [SerializeField] private TMP_Dropdown _chatDropdown;
    [SerializeField] private Button _sendButton;

    [SerializeField] private ChatBubble chatBubblePrefab;
    [SerializeField] private int ChatBubblePoolSize = 30;
    [SerializeField] private ScrollRect _scrollRect;
    [SerializeField] private Transform _chatContentParent; // ChatBubble의 부모 (ScrollView Content)
    private int _currentIndex = 0; // 가장 오래된 버블 위치 추적

 
    private ChatType _currentChatType = ChatType.GLOBAL;
    private List<ChatBubble> _chatBubbles = new List<ChatBubble>();
    
    private void Start()
    {
        _chatBubbles.Clear();
        CreateChatBubble();
        
        _inputField.onSubmit.AddListener(OnSummitChatting);
        _sendButton.onClick.AddListener(OnClickSendButton);
        _chatDropdown.onValueChanged.AddListener(OnChatDropdownValueChanged);
    }

    private void CreateChatBubble()
    {
        for (int i = 0; i < ChatBubblePoolSize; i++)
        {
            var bubble = Instantiate(chatBubblePrefab, _chatContentParent);
            bubble.gameObject.SetActive(false);
            _chatBubbles.Add(bubble);
        }
    }

    public void AppendChatMessage(string sender, string message)
    {
        var bubble = _chatBubbles[_currentIndex];

        bubble.SetMessage(sender, message);
        bubble.transform.SetSiblingIndex(_chatContentParent.childCount - 1);
        bubble.gameObject.SetActive(true);

        _currentIndex = (_currentIndex + 1) % ChatBubblePoolSize;

        // 다음 프레임에서 스크롤 아래로
        Canvas.ForceUpdateCanvases(); // 즉시 UI 갱신
        _scrollRect.verticalNormalizedPosition = 0f; // 맨 아래로 스크롤
    }
    
    private void OnChatDropdownValueChanged(int index)
    {
        _currentChatType = (ChatType)index;
    }

    private void OnClickSendButton()
    {
        SendChatMessage(_inputField.text);
        _inputField.text = "";
    }

    private void OnSummitChatting(string message)
    {
        SendChatMessage(message);
        _inputField.text = "";
    }

    private void SendChatMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        string receiver = "ALL";
        string content = message;

        if (_currentChatType == ChatType.WHISPER)
        {
            if (message.StartsWith("/"))
            {
                int firstSpaceIndex = message.IndexOf(' ');
                if (firstSpaceIndex > 1)
                {
                    receiver = message.Substring(1, firstSpaceIndex - 1);
                    content = message.Substring(firstSpaceIndex + 1);
                }
                else
                {
                    Debug.LogWarning("귓속말 형식이 올바르지 않습니다. 예: /닉네임 메시지");
                    return;
                }
            }
            else
            {
                Debug.LogWarning("귓속말은 '/'로 시작해야 합니다. 예: /닉네임 메시지");
                return;
            }
        }

        var chat = new ChatPacket
        {
            sender = GameManager.Instance.NickName.Value,
            receiver = receiver,
            message = content,
            chatType = _currentChatType
        };
        _ = NetworkManager.Instance.SendPacket(chat);
    }

    
    
}