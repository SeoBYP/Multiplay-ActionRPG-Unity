using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.GUI.Common
{
    public class WarningPopup : BasePopup
    {
        [SerializeField] private TextMeshProUGUI title;
        [SerializeField] private TextMeshProUGUI message;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;

        private Action _onConfirm;
        private Action _onCancel;

        public void Setup(string titleText, string messageText,
            Action onConfirm, Action onCancel = null,
            PopupGlowType glow = PopupGlowType.Danger)
        {
            title.text   = titleText;
            message.text = messageText;
            _onConfirm   = onConfirm;
            _onCancel    = onCancel;

            confirmButton.onClick.RemoveAllListeners();
            cancelButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(OnConfirmClicked);
            cancelButton.onClick.AddListener(OnCancelClicked);

            ApplyGlow(glow);
        }

        private void OnConfirmClicked() { _onConfirm?.Invoke(); Close(); }
        private void OnCancelClicked()  { _onCancel?.Invoke();  Close(); }
    }
}
