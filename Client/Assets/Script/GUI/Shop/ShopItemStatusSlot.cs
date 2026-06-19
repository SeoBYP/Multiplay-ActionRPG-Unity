using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.GUI.Shop
{
    /// <summary>선택 패널의 스탯 미리보기 한 줄(예: 공격력 +5). Shop View 가 Bind.</summary>
    public class ShopItemStatusSlot : MonoBehaviour
    {
        [SerializeField] private Image status_Icon;
        [SerializeField] private TextMeshProUGUI status_Amount;

        /// <summary>스탯 한 줄 텍스트 표시(아이콘 매핑은 추후 — 지금은 텍스트만).</summary>
        public void Bind(string label)
        {
            if (status_Amount != null)
                status_Amount.text = label;
        }
    }
}
