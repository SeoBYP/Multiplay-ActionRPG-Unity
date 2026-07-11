using UnityEngine;
using UnityEngine.UI;

namespace Game.Gameplay.Character
{
    /// <summary>
    /// 락온 대상 위에 뜨는 월드공간 표시(연출 전용). <see cref="LockOnDriver"/> 가 한 번 생성해 재사용하며
    /// Show(target)/Hide 로 대상에 붙인다. 매 프레임 대상 머리 위로 따라가며 카메라를 향해 빌보드한다.
    /// (MonsterHealthBar 빌보드 패턴과 동일. 판정·게임플레이엔 관여하지 않는다.)
    /// </summary>
    public sealed class LockOnMarker : MonoBehaviour
    {
        private const float HeightOffset = 2.3f;

        private Transform _target;
        private Transform _cam;
        private static Sprite _iconSprite;

        /// <summary>런타임 안전한 조준점 스프라이트(빌트인 UI 리소스는 플레이어 빌드에 없어 못 씀). 링 형태로 1회 생성·캐시.</summary>
        private static Sprite IconSprite()
        {
            if (_iconSprite != null) return _iconSprite;
            const int size = 48;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            var center = new Vector2(size / 2f, size / 2f);
            float outer = size * 0.46f, inner = size * 0.30f;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    px[y * size + x] = (d <= outer && d >= inner) ? Color.white : new Color(1f, 1f, 1f, 0f); // 링
                }
            tex.SetPixels(px);
            tex.Apply();
            _iconSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            _iconSprite.hideFlags = HideFlags.HideAndDontSave;
            return _iconSprite;
        }

        /// <summary>런타임에 월드공간 캔버스+아이콘을 만들어 붙인 마커 GameObject 를 반환한다.</summary>
        public static LockOnMarker Create()
        {
            var go = new GameObject("LockOnMarker", typeof(RectTransform), typeof(Canvas));
            go.transform.localScale = Vector3.one * 0.008f;
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(48f, 48f);

            var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            icon.transform.SetParent(go.transform, false);
            var ir = icon.GetComponent<RectTransform>();
            ir.anchorMin = Vector2.zero; ir.anchorMax = Vector2.one; ir.offsetMin = Vector2.zero; ir.offsetMax = Vector2.zero;
            var img = icon.GetComponent<Image>();
            img.sprite = IconSprite(); // 절차적 링(런타임 안전)
            img.color = new Color(1f, 0.85f, 0.2f, 0.95f); // 노란 조준점
            img.raycastTarget = false;

            var marker = go.AddComponent<LockOnMarker>();
            go.SetActive(false);
            return marker;
        }

        public void Show(Transform target)
        {
            _target = target;
            gameObject.SetActive(target != null);
        }

        public void Hide()
        {
            _target = null;
            gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (_target == null) { Hide(); return; }
            if (_cam == null)
            {
                var mainCam = UnityEngine.Camera.main;
                if (mainCam == null) return;
                _cam = mainCam.transform;
            }
            transform.position = _target.position + Vector3.up * HeightOffset;
            transform.rotation = Quaternion.LookRotation(transform.position - _cam.position);
        }
    }
}
