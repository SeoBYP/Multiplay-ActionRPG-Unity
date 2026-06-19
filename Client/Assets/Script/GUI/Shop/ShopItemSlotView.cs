using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.GUI.Shop
{
    /// <summary>상점 리스트의 한 칸. 아이콘·이름 + 선택 버튼. ShopModel 데이터를 Shop View 가 Bind.</summary>
    public class ShopItemSlotView : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private Button sellectButton;

        /// <summary>한 항목을 표시하고 선택 콜백을 건다. onSelect = 클릭 시 ShopIntent.SelectItem.</summary>
        public void Bind(string itemId, Sprite iconSprite, string displayName, Action onSelect)
        {
            if (icon != null)
            {
                icon.sprite = iconSprite;
                icon.enabled = iconSprite != null;
            }
            if (nameText != null)
                nameText.text = displayName;

            if (sellectButton != null)
            {
                sellectButton.onClick.RemoveAllListeners();
                sellectButton.onClick.AddListener(() => onSelect?.Invoke());
            }
        }
    }
}
