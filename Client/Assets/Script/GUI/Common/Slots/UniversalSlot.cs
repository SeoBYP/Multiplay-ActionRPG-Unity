using UnityEngine;

namespace Game.GUI.Common
{
    /// <summary>
    /// 슬롯 Container. 빈 칸일 때는 컨테이너만 보이고, 아이템이 있을 때만 Content(ItemContentsSlot)를
    /// 동적 생성해 표시한다. 도메인/모델을 모른다 — 부모 View가 content prefab과 값을 넘긴다(generic 유지).
    ///
    /// 슬롯 자체(컨테이너)는 항상 활성. 탭/정렬/내용이 바뀌면 Content만 교체/숨김한다.
    /// 타입별 슬롯 디자인 시 다른 prefab이 요청되면 기존 Content를 파괴하고 새로 생성한다.
    /// </summary>
    public class UniversalSlot : MonoBehaviour
    {
        [Tooltip("Content(ItemContentsSlot)가 생성될 부모. 비우면 자기 transform 아래에 생성.")]
        [SerializeField] private Transform contentParent;

        private ItemContentsSlot _content;
        private ItemContentsSlot _contentPrefab; // 현재 content가 어떤 prefab으로 생성됐는지(타입 교체 감지)

        private Transform ContentParent => contentParent != null ? contentParent : transform;

        /// <summary>현재 Content(없으면 null).</summary>
        public ItemContentsSlot Content => _content;

        /// <summary>
        /// 아이템 표시 — prefab으로 Content를 보장(없거나 다른 prefab이면 재생성)·활성화 후 반환한다.
        /// 반환값에 부모 View가 Bind(icon, qty)를 호출한다.
        /// </summary>
        public ItemContentsSlot EnsureContent(ItemContentsSlot prefab)
        {
            if (prefab == null)
                return null;

            // 다른 타입(prefab)이 요청되면 기존 Content 파괴 후 재생성.
            if (_content != null && _contentPrefab != prefab)
            {
                Destroy(_content.gameObject);
                _content = null;
            }

            if (_content == null)
            {
                _content = Instantiate(prefab, ContentParent);
                _contentPrefab = prefab;
            }

            if (!_content.gameObject.activeSelf)
                _content.gameObject.SetActive(true);

            return _content;
        }

        /// <summary>빈 슬롯 — Content를 숨긴다(인스턴스는 재사용 위해 유지).</summary>
        public void ClearContent()
        {
            if (_content != null && _content.gameObject.activeSelf)
                _content.gameObject.SetActive(false);
        }
    }
}
