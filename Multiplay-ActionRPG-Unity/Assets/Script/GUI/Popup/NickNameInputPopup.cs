using System;
using Game.Managers;
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
        GameManager.Instance.NickName.Value = nicknameInputField.text;
        Deactivate();
    }

    protected override void OnActivate()
    {
        
    }

    protected override void OnDeactivate()
    {

    }
}
