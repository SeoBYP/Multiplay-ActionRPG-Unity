using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.GUI.Common
{
    /// <summary>
    /// 공통 아이템 슬롯 — 인벤토리/상점이 동일 컴포넌트로 사용(통합). 표시 전용(도메인/모델 비참조).
    /// 아이콘 + (수량|이름) + 등급 배경 + 클릭 버튼. 프리팹마다 쓰는 필드만 할당하면 된다
    /// (그리드=icon+count / 리스트=icon+name). 미할당 필드는 무시한다.
    ///   - 수량(count) 1 이하 → 숨김(스택 표기 관례), 이름(displayName) null → 숨김.
    ///   - 등급 배경(gradeBackground sprite)을 넘기면 background Image 에 스프라이트로 적용(색 아님 — 호출자가 해석).
    /// </summary>
    public class ItemContentsSlot : MonoBehaviour
    {
        [SerializeField] private Image itemIcon;
        [SerializeField] private TextMeshProUGUI itemCount;
        [SerializeField] private TextMeshProUGUI itemName;       // 리스트형(상점) 표시용 — 그리드(인벤)에선 미할당.
        [SerializeField] private Button itemButton;
        [SerializeField] private Image gradeBackground;          // 등급 배경(선택) — 미할당이면 무시. Sprite 는 호출자가 해석.

        private string _itemId;
        private Action<string> _onClick;
        private bool _wired;

        /// <summary>
        /// 슬롯 1칸 바인딩. count≤1=수량 숨김, displayName=null=이름 숨김, gradeBackground=null=배경 끔.
        /// 슬롯은 도메인/enum 을 모르고 Sprite·문자열만 받는다(decoupled — grade 해석은 Model 책임).
        /// </summary>
        public void Bind(string itemId, Sprite icon, int count, Action<string> onClick = null,
            Sprite gradeBackgroundSprite = null, string displayName = null)
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

            if (itemName != null)
            {
                itemName.text = displayName ?? string.Empty;
                itemName.gameObject.SetActive(!string.IsNullOrEmpty(displayName));
            }

            if (gradeBackground != null)
            {
                gradeBackground.sprite = gradeBackgroundSprite;
                gradeBackground.enabled = gradeBackgroundSprite != null;
            }
        }
    }
}
