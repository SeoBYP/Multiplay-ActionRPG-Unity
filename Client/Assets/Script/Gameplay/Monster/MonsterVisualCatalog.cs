using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Monster
{
    /// <summary>
    /// monsterId → 표시 프리팹 매핑(클라 전용, 표시 진실원). "몬스터가 무엇인가"(스탯) = `MonsterCatalogDefinition`(서버 권위),
    /// "어떻게 보이나"(모델·애니) = 여기. 서버는 이 카탈로그를 모른다(비주얼은 클라 관심사).
    ///
    /// 각 프리팹은 _DLNK 모델 + 자체 Animator(컨트롤러) + `MonsterEntity`(애니 상태이름 직렬화) + `MonsterHealthBar` 구성.
    /// `MonsterSpawner` 가 S_SpawnMonster.MonsterId 로 프리팹을 고른다(미등록이면 기본 프리팹 폴백).
    /// </summary>
    [CreateAssetMenu(fileName = "MonsterVisualCatalog", menuName = "Game/Monster Visual Catalog", order = 5)]
    public sealed class MonsterVisualCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [Tooltip("서버·클라 공용 monsterId (MonsterCatalogDefinition 과 동일 키).")]
            public string monsterId;
            [Tooltip("표시 프리팹(_DLNK 모델 + Animator + MonsterEntity + 체력바).")]
            public GameObject prefab;
        }

        public List<Entry> entries = new();

        /// <summary>monsterId 의 표시 프리팹. 미등록이면 null(스포너가 기본 프리팹으로 폴백).</summary>
        public GameObject GetPrefab(string monsterId)
        {
            foreach (var e in entries)
                if (e != null && e.monsterId == monsterId)
                    return e.prefab;
            return null;
        }
    }
}
