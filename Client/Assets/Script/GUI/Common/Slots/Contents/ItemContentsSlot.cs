using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.GUI.Common
{
    /// <summary>
    /// Universal Slot이 Item을 표시할 때 사용하는 Slot.
    /// 아이콘 + 수량을 그리고(표시 전용, 도메인/모델 비참조), itemButton 클릭 시 itemId 를 부모 View 콜백으로 전달한다.
    /// </summary>
    public class ItemContentsSlot : MonoBehaviour
    {
        [SerializeField] private Image itemIcon;
        [SerializeField] private TextMeshProUGUI itemCount;
        [SerializeField] private Button itemButton;

        private string _itemId;
        private Action<string> _onClick;
        private bool _wired;

        /// <summary>아이콘·수량·itemId·클릭 콜백을 바인딩한다. 수량 1 이하는 텍스트 숨김(스택 표기 관례).</summary>
        public void Bind(string itemId, Sprite icon, int count, Action<string> onClick = null)
        {
            _itemId  = itemId;
            _onClick = onClick;

            // 클릭 → 최신 _itemId 를 콜백으로 전달. 리스너는 1회만 등록(재바인딩 시 중복 등록 방지).
            if (itemButton != null && !_wired)
            {
                itemButton.onClick.AddListener(() => _onClick?.Invoke(_itemId));
                _wired = true;
            }

            if (itemIcon != null)
            {
                itemIcon.sprite = icon;
                itemIcon.enabled = icon != null;
            }

            if (itemCount != null)
                itemCount.text = count > 1 ? count.ToString() : string.Empty;
        }
    }
}
