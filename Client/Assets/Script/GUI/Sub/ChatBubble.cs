using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Script.GUI.Sub
{
    public class ChatBubble : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI sender;
        [SerializeField] private TextMeshProUGUI message;

        public void SetMessage(string sender, string message)
        {
            this.sender.text = $"<b>{sender}</b>";
            this.message.text = message;
            
            LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
        }
    }
}