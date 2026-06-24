using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Script.GUI.Inventory
{
    /// <summary>
    /// 슬롯 클릭 시 슬롯 오른쪽에 뜨는 액션 팝업. Canvas 직속으로 생성되며(슬롯의 자식 아님),
    /// 위치만 슬롯 기준으로 계산해 배치한다.
    ///   - 소모품: 사용(useButton) 활성 / 장착(equipButton) 비활성
    ///   - 장비:   장착(equipButton) 활성 / 사용(useButton) 비활성
    /// 버튼 클릭 → 콜백 호출 후 닫기 요청. 닫기(파괴)·백드롭 정리는 부모 View(Inventory)가 OnCloseRequested 구독으로 처리.
    /// </summary>
    public class ItemActionPanel : UIBehaviour
    {
        [SerializeField] private Button useButton;
        [SerializeField] private Button equipButton;
        [SerializeField] private Button unEquipButton;
        [SerializeField] private Button sellButton;

        private string _itemId;
        private Action<string> _onUse;
        private Action<string> _onEquip;
        private Action<string> _onUnequip;
        private Action<string> _onSell;

        /// <summary>닫기 요청(버튼 사용 후). View 가 구독해 팝업+백드롭을 파괴한다.</summary>
        public event Action OnCloseRequested;

        /// <summary>
        /// itemId 와 콜백을 바인딩한다. canUse/canEquip/canUnequip 로 각 버튼의 활성을 결정(호출처가 결정).
        ///   - 인벤토리: 소모품=use, 장비=equip / 장비창: unequip
        /// 클릭 → 콜백(itemId) + 닫기 요청.
        /// </summary>
        public void Bind(string itemId,
            Action<string> onUse, Action<string> onEquip, Action<string> onUnequip, Action<string> onSell,
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
