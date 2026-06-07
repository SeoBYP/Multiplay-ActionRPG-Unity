using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.GUI.Common
{
    /// <summary>
    /// Universal Slot이 Item을 표시할 때 사용하는 Slot.
    /// 아이콘 + 수량만 그린다(표시 전용, 도메인/모델 비참조 — 부모 View가 값을 주입).
    /// </summary>
    public class ItemContentsSlot : MonoBehaviour
    {
        [SerializeField] private Image itemIcon;
        [SerializeField] private TextMeshProUGUI itemCount;

        /// <summary>아이콘과 수량을 바인딩한다. 수량 1 이하는 텍스트를 숨긴다(스택 표기 관례).</summary>
        public void Bind(Sprite icon, int count)
        {
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
