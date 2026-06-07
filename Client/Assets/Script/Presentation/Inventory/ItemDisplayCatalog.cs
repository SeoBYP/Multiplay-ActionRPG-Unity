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
            public string itemId;
            public string displayName;
            public Sprite icon;
            public ItemCategory category;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        private Dictionary<string, Entry> _byId;

        public Entry Get(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
                return null;

            _byId ??= BuildIndex();
            return _byId.GetValueOrDefault(itemId);
        }

        private Dictionary<string, Entry> BuildIndex()
        {
            var dict = new Dictionary<string, Entry>(entries.Length);
            foreach (var e in entries)
            {
                if (e != null && !string.IsNullOrEmpty(e.itemId))
                    dict[e.itemId] = e;
            }
            return dict;
        }
    }
}
