using Game.Managers;
using Game.Network;
using ServerCore.Protocol;
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
        
        NetworkManager.Instance.Dispatcher.Common.OnSetNicknameReceived.AddListener(OnSetNicknameReceived);
    }

    private void OnCancelButtonClick()
    {
        Deactivate();
    }
    
    private void OnSetNicknameReceived(S_SetNickname response)
    {
        if (response.Success)
        {
            GameManager.Instance.NickName.Value = response.Nickname;
            Deactivate();
        }
    }

    private void OnConfirmButtonClick()
    {
        var nickname = nicknameInputField.text.Trim();

        if (string.IsNullOrWhiteSpace(nickname))
        {
            Debug.LogWarning("[닉네임 입력] 닉네임이 비어있습니다.");
            return;
        }

        var request = new C_SetNickname();
        request.Nickname = nickname;
        
        var packet = new Packet();
        packet.CSetNickname = request;
        NetworkManager.Instance.SendPacket(packet);
    }

    protected override void OnActivate() { }

    protected override void OnDeactivate() { }
}