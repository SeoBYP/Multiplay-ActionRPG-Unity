using System;
using Game.Managers;
using Game.Network;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NickNameInputPopup : CanvasUIBehaviour
{
    [SerializeField] private TMP_InputField nicknameInputField;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    protected override void Start()
    {
        confirmButton.onClick.AddListener(OnConfirmButtonClick);
        cancelButton.onClick.AddListener(OnCancelButtonClick);
    }

    private void OnCancelButtonClick()
    {
        Deactivate();
        
    }

    private void OnConfirmButtonClick()
    {
        var nickname = nicknameInputField.text.Trim();

        if (string.IsNullOrWhiteSpace(nickname))
        {
            Debug.LogWarning("[닉네임 입력] 닉네임이 비어있습니다.");
            return;
        }

        var packet = new C_SetNicknamePacket
        {
            nickname = nickname
        };

        _ = NetworkManager.Instance.SendPacket(packet);
    }

    protected override void OnActivate() { }

    protected override void OnDeactivate() { }
}