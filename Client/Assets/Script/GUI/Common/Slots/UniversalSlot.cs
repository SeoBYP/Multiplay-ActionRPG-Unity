using UnityEngine;

namespace Game.GUI.Common
{
    /// <summary>
    /// 슬롯 Container. 자식에 붙는 Content Slot(ItemContentsSlot 등)을 관리한다.
    /// 도메인/모델을 알지 않는다 — 부모 View가 ItemContents에 값을 바인딩한다(generic 유지).
    /// </summary>
    public class UniversalSlot : MonoBehaviour
    {
        [SerializeField] private ItemContentsSlot itemContents;

        /// <summary>아이템 표시 컨텐츠 슬롯. 부모 View가 Bind 한다.</summary>
        public ItemContentsSlot ItemContents
        {
            get
            {
                if (itemContents == null)
                    itemContents = GetComponentInChildren<ItemContentsSlot>(true);
                return itemContents;
            }
        }

        /// <summary>슬롯을 표시하고 컨텐츠를 켠다.</summary>
        public void Show()
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            var contents = ItemContents;
            if (contents != null && !contents.gameObject.activeSelf)
                contents.gameObject.SetActive(true);
        }

        /// <summary>슬롯을 숨긴다(빈 칸).</summary>
        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }
    }
}
