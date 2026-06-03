using System;
using System.Collections.Generic;
using Script.System.GamePlayAbilitySystem;
using UnityEngine;

namespace Game.Presentation.InGame
{
    /// <summary>
    /// 버프/디버프 **표시 전용** 매핑 테이블 (진실원 아님).
    ///   Category → Sprite (같은 카테고리는 같은 아이콘)
    ///   buff/debuff 색 (polarity로 선택)
    /// 서버·Shared.Gameplay는 이 에셋을 모른다. EffectId/Category만 보고 여기서 그림으로 해석한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Effect Icon Catalog", fileName = "EffectIconCatalog")]
    public sealed class EffectIconCatalog : ScriptableObject
    {
        [Serializable]
        private struct Entry
        {
            public EEffectCategory category;
            public Sprite icon;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();
        [SerializeField] private Color buffColor = new Color(0.4f, 1f, 0.4f);
        [SerializeField] private Color debuffColor = new Color(1f, 0.4f, 0.4f);

        private Dictionary<EEffectCategory, Sprite> _map;

        public Sprite GetIcon(EEffectCategory category)
        {
            _map ??= Build();
            return _map.TryGetValue(category, out var sprite) ? sprite : null;
        }

        public Color GetColor(bool isBuff) => isBuff ? buffColor : debuffColor;

        /// <summary>테스트/툴에서 런타임 아이콘 등록용.</summary>
        public void RegisterIcon(EEffectCategory category, Sprite icon)
        {
            _map ??= Build();
            _map[category] = icon;
        }

        private Dictionary<EEffectCategory, Sprite> Build()
        {
            var map = new Dictionary<EEffectCategory, Sprite>();
            foreach (var e in entries)
                map[e.category] = e.icon;
            return map;
        }
    }
}
