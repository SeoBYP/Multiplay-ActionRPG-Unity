using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.GUI.Common
{
    public class AlertPopup : BasePopup
    {
        [SerializeField] private TextMeshProUGUI title;
        [SerializeField] private TextMeshProUGUI message;
        [SerializeField] private Button okButton;

        private Action _onOk;

        public void Setup(string titleText, string messageText,
            Action onOk = null, PopupGlowType glow = PopupGlowType.Info)
        {
            title.text   = titleText;
            message.text = messageText;
            _onOk        = onOk;

            okButton.onClick.RemoveAllListeners();
            okButton.onClick.AddListener(OnOkClicked);

            ApplyGlow(glow);
        }

        private void OnOkClicked()
        {
            _onOk?.Invoke();
            Close();
        }
    }
}
