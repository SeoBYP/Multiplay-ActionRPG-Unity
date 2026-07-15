using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Monster
{
    /// <summary>
    /// 몬스터 타입별 정의 저작(authoring) 진실원. 디자이너가 이 SO 하나를 Inspector 에서 편집한다.
    /// (DropTableDefinition·LevelTableDefinition 과 동일 컨벤션 — 단일 SO + List, SO 저작 → JSON bake → 서버 임베디드.)
    ///
    /// "몬스터가 무엇인가"(스탯·exp) = 여기. "몬스터가 어디에 스폰되나"(위치·슬롯) = MapDefinition 의 스폰 데이터.
    /// 서버(UnityEngine 의존 0)는 못 읽으므로 Export 툴(Tools/Monster/Export)이 monsters.json 으로 bake →
    /// `Shared.Infrastructure.Monsters.MonsterCatalog` 가 읽는다(던전 시뮬 스탯 + Main 킬 exp).
    /// </summary>
    [CreateAssetMenu(fileName = "MonsterCatalogDefinition", menuName = "Game/Monster Catalog Definition", order = 4)]
    public sealed class MonsterCatalogDefinition : ScriptableObject
    {
        [Tooltip("몬스터 타입별 정의. monsterId 는 서버/클라 공용 식별자(예: creepy_demon).")]
        public List<MonsterDefinition> monsters = new();

        /// <summary>monsterId 의 정의. 미등록이면 null. (클라 런타임 조회용)</summary>
        public MonsterDefinition Get(string monsterId)
        {
            foreach (var m in monsters)
                if (m.monsterId == monsterId)
                    return m;
            return null;
        }
    }

    /// <summary>한 몬스터 타입 — 시뮬 스탯 + 보상(exp).</summary>
    [Serializable]
    public sealed class MonsterDefinition
    {
        [Tooltip("몬스터 타입 키(서버·클라 공용).")]
        public string monsterId;

        [Header("Sim 스탯 (던전 서버 시뮬)")]
        public int maxHp = 30;
        public float moveSpeed = 2.0f;
        public float aggroRange = 6f;
        public float attackRange = 1.2f;
        public float attackCooldownMs = 1500f;
        public int attackDamage = 5;

        [Tooltip("적중 시 부여할 CC 효과 id(GameplayEffectCatalog). 비우면 없음. 예: slow_3s · stun_1_5s")]
        public string onHitEffectId = "";

        [Header("보상")]
        [Tooltip("처치 시 획득 경험치(Main 킬 보상). 던전은 맵 클리어 단위라 미사용 가능.")]
        public int expReward = 20;
    }
}
