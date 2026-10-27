using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Script.GUI.Inventory
{
    /// <summary>
    /// 슬롯 클릭 시 슬롯 오른쪽에 뜨는 액션 팝업(Canvas 직속, 위치만 슬롯 기준).
    /// 버튼 클릭 → 콜백 호출 후 닫기 요청. 파괴·백드롭 정리는 ItemActionPanelController 가 OnCloseRequested 구독으로 처리.
    /// </summary>
    public class ItemActionPanel : UIBehaviour
    {
        [SerializeField] private Button useButton;
        [SerializeField] private Button equipButton;
        [SerializeField] private Button unEquipButton;
        [SerializeField] private Button sellButton;

        private int _itemId;
        private Action<int> _onUse;
        private Action<int> _onEquip;
        private Action<int> _onUnequip;
        private Action<int> _onSell;

        /// <summary>닫기 요청(버튼 사용 후). 구독자가 팝업+백드롭을 파괴한다.</summary>
        public event Action OnCloseRequested;

        /// <summary>
        /// itemId 와 콜백을 바인딩한다. can* 로 각 버튼 활성을 호출처가 정한다. 클릭 → 콜백(itemId) + 닫기 요청.
        /// </summary>
        public void Bind(int itemId,
            Action<int> onUse, Action<int> onEquip, Action<int> onUnequip, Action<int> onSell,
            bool canUse, bool canEquip, bool canUnequip, bool canSell)
        {
            _itemId = itemId;
            _onUse = onUse;
            _onEquip = onEquip;
            _onUnequip = onUnequip;
            _onSell = onSell;

            WireButton(useButton, canUse, () => _onUse?.Invoke(_itemId));
            WireButton(equipButton, canEquip, () => _onEquip?.Invoke(_itemId));
            WireButton(unEquipButton, canUnequip, () => _onUnequip?.Invoke(_itemId));
            WireButton(sellButton, canSell, () => _onSell?.Invoke(_itemId));
        }

        private void WireButton(Button button, bool active, Action onClick)
        {
            if (button == null) return;

            button.gameObject.SetActive(active);
            button.onClick.RemoveAllListeners();
            if (!active) return;

            button.onClick.AddListener(() =>
            {
                onClick?.Invoke();
                OnCloseRequested?.Invoke();
            });
        }
    }
}
