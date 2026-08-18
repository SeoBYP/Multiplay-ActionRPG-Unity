using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Presentation.Inventory
{
    /// <summary>
    /// 클라 아이템 표시 카탈로그(ScriptableObject). itemId → 이름·아이콘·분류.
    /// 서버 ItemCatalog(정의)와 itemId로 정렬되는 클라 미러 — 디자이너가 Unity에서 스프라이트를 할당한다.
    /// (서버 proto는 itemId+수량만 보내므로, 표시 데이터는 이 카탈로그가 소유.)
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Inventory/Item Display Catalog", fileName = "ItemDisplayCatalog")]
    public sealed class ItemDisplayCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [Tooltip("서버·DB·패킷이 쓰는 키. ItemCatalogDefinition 의 numericId 와 같아야 한다.")]
            public int numericId;

            [Tooltip("저작·로그용 문자열 키. 식별자가 아니라 사람이 읽기 위한 이름이다.")]
            public string itemId;
            public string displayName;
            [TextArea(2, 4)] public string description; // 상점 선택 패널 등 표시용 설명(플레이버/용도). 비우면 빈 문자열.
            public Sprite icon;
            public ItemCategory category;

            // 등급(레어도) — 표시 색/도감 정렬용 미러. 디자이너가 서버 ItemDef.Grade 와 맞춰 인스펙터에서 지정.
            // 기본 Common(미지정 안전값). 게임플레이 무효과(현재 표시 전용).
            public ItemGrade grade = ItemGrade.Common;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        private Dictionary<int, Entry> _byNumericId;

        /// <summary>전체 정의(도감 — 미발견 포함 전체 목록·완성도 산출용).</summary>
        public IReadOnlyList<Entry> All => entries;

        /// <summary>
        /// numericId 로 표시 정보를 찾는다. 서버·DB·패킷이 전부 int 키를 쓰므로 조회도 int 다.
        /// <para>문자열 <c>itemId</c> 는 저작·로그용으로 남겨두되 <b>조회 키가 아니다</b> — 키가 둘이면
        /// 다시 갈라진다(items.json 이 Exporter 없이 이중 저작되며 갈라졌던 A4 의 재발 방지).</para>
        /// </summary>
        public Entry Get(int numericId)
        {
            if (numericId <= 0)
                return null;

            _byNumericId ??= BuildIndex();
            return _byNumericId.GetValueOrDefault(numericId);
        }

        private Dictionary<int, Entry> BuildIndex()
        {
            var dict = new Dictionary<int, Entry>(entries.Length);
            foreach (var e in entries)
            {
                if (e != null && e.numericId > 0)
                    dict[e.numericId] = e;
            }
            return dict;
        }
    }
}
