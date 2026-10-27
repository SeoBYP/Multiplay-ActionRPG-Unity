using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.GUI.Common
{
    /// <summary>
    /// 화면 전체를 덮는 투명 클릭 캐처. 팝업 뒤(sibling 아래)에 깔아, 팝업 바깥 클릭 시 Clicked 를 발행한다.
    /// 런타임 생성용 — Create(parent).
    /// </summary>
    public sealed class BackDropButton : UIBehaviour, IPointerClickHandler
    {
        /// <summary>뒤 화면(백드롭) 클릭 시 발행.</summary>
        public event Action Clicked;

        public void OnPointerClick(PointerEventData eventData) => Clicked?.Invoke();

        public static BackDropButton Create(Transform parent)
        {
            var go = new GameObject("BackDropButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(BackDropButton));
            go.transform.SetParent(parent, worldPositionStays: false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;

            var img = go.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f); // 투명하지만 raycastTarget=true → 클릭은 받는다
            img.raycastTarget = true;

            return go.GetComponent<BackDropButton>();
        }
    }
}
