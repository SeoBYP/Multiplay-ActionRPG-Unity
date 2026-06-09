using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.GUI.Common
{
    /// <summary>
    /// 화면 전체를 덮는 투명 클릭 캐처. 팝업(ItemActionPanel 등) 뒤에 깔아, 팝업 바깥(=뒤 화면) 클릭 시
    /// Clicked 를 발행해 팝업을 닫게 한다. 팝업보다 sibling 아래(=뒤)에 두고 팝업을 그 위에 올린다.
    /// 런타임 생성용 — Create(parent)로 RectTransform(stretch)+투명 Image(raycastTarget)를 갖춘 GO를 만든다.
    /// </summary>
    public sealed class BackDropButton : UIBehaviour, IPointerClickHandler
    {
        /// <summary>뒤 화면(백드롭) 클릭 시 발행.</summary>
        public event Action Clicked;

        public void OnPointerClick(PointerEventData eventData) => Clicked?.Invoke();

        /// <summary>parent(보통 Canvas) 아래에 풀스크린 투명 백드롭을 생성한다.</summary>
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
