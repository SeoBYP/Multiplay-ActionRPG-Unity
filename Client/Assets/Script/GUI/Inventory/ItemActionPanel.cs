using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Script.GUI.Inventory
{
    /// <summary>
    /// 슬롯 클릭 시 슬롯 오른쪽에 뜨는 액션 팝업(사용 등). Canvas 직속으로 생성되며(슬롯의 자식 아님),
    /// 위치만 슬롯 기준으로 계산해 배치한다. useButton 클릭 → onUse(itemId) 호출 후 닫기 요청.
    /// 닫기(파괴)·백드롭 정리는 부모 View(Inventory)가 OnCloseRequested 구독으로 처리한다.
    /// </summary>
    public class ItemActionPanel : UIBehaviour
    {
        [SerializeField] private Button useButton;

        private string _itemId;
        private Action<string> _onUse;

        /// <summary>닫기 요청(useButton 사용 후). View 가 구독해 팝업+백드롭을 파괴한다.</summary>
        public event Action OnCloseRequested;

        /// <summary>itemId 와 사용 콜백을 바인딩한다. useButton → onUse(itemId) + 닫기 요청.</summary>
        public void Bind(string itemId, Action<string> onUse)
        {
            _itemId = itemId;
            _onUse  = onUse;

            if (useButton != null)
            {
                useButton.onClick.RemoveAllListeners();
                useButton.onClick.AddListener(() =>
                {
                    _onUse?.Invoke(_itemId);
                    OnCloseRequested?.Invoke();
                });
            }
        }
    }
}
