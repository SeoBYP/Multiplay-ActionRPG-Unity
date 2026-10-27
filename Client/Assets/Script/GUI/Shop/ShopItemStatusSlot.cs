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

        public void Bind(string label)
        {
            if (status_Amount != null)
                status_Amount.text = label;
        }
    }
}
