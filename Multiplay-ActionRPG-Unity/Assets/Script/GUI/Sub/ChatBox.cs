using System;
using Game.Managers;
using Game.Network;
using TMPro;
using UnityEngine;

public class ChatBox : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _nickNameText;
    [SerializeField] private TMP_InputField _inputField;

    private void Start()
    {
        _nickNameText.text = "UnityClient";
        _inputField.onSubmit.AddListener(OnSummitChating);
    }

    private void OnSummitChating(string message)
    {
        var chat = new ChatPacket
        {
            sender = _nickNameText.text,
            receiver = "ALL",
            message = message,
            chatType = ChatType.GLOBAL
        };
        _ = NetworkManager.Instance.SendPacket(chat);
    }
    
    
}