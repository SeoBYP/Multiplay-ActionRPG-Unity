using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Monster
{
    /// <summary>
    /// 몬스터 등급 분류(저작용). 서버 <c>Shared.Infrastructure.Monsters.MonsterTier</c> 의 미러 —
    /// 그쪽은 서버 전용 어셈블리라 클라가 참조할 수 없다. <b>계약은 JSON 의 문자열</b>("Normal"/"Elite"/"Boss").
    /// <para>문자열인 이유: JSON 을 사람이 읽을 때 <c>2</c> 보다 <c>"Boss"</c> 가 낫고, 등급이 늘어도 숫자 재매핑이 없다.</para>
    /// </summary>
    public enum MonsterTierId
    {
        Normal = 0,
        Elite = 1,
        Boss = 2,
    }

    /// <summary>
    /// 몬스터 타입별 정의 저작(authoring) 진실원. 디자이너가 이 SO 하나를 Inspector 에서 편집한다.
    /// (DropTableDefinition·LevelTableDefinition 과 동일 컨벤션 — 단일 SO + List, SO 저작 → JSON bake → 서버 임베디드.)
    ///
    /// "몬스터가 무엇인가"(스탯·exp·등급) = 여기. "몬스터가 어디에 스폰되나"(위치·레벨) = MapDefinition 의 스폰 데이터.
    /// 서버(UnityEngine 의존 0)는 못 읽으므로 Export 툴(Tools/Monster/Export)이 monsters.json 으로 bake →
    /// `Shared.Infrastructure.Monsters.MonsterCatalog` 가 읽는다(던전 시뮬 스탯 + Main 킬 exp).
    ///
    /// <para><b>변종 = 별개 행</b>(AC-G). <c>leviathan</c> 과 <c>leviathan_boss</c> 는 각자 ID·스탯을 갖는다 —
    /// 배율 테이블로 곱하지 않는다. 스폰은 <b>monsterId 하나만</b> 처리하면 된다.</para>
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

    /// <summary>한 몬스터 타입 — "무엇인가"(체력·이동·시야) + 보상(exp) + 쓸 수 있는 어빌리티 목록.</summary>
    [Serializable]
    public sealed class MonsterDefinition
    {
        [Tooltip("몬스터 타입 키(서버·클라 공용).")]
        public string monsterId;

        [Header("Sim 스탯 (던전 서버 시뮬)")]
        public int maxHp = 30;
        public float moveSpeed = 2.0f;
        [Tooltip("추격 시작 거리. 공격 사거리는 어빌리티(activationRange)가 갖는다.")]
        public float aggroRange = 6f;

        [Header("어빌리티 (AC-B B4 — 공격은 전부 Ability SO 저작)")]
        [Tooltip("이 몬스터가 쓰는 어빌리티 id 목록(AbilityCatalogDefinition 의 id). **우선순위 = 이 순서** —\n" +
                 "서버 AI 가 사거리·쿨다운을 만족하는 첫 어빌리티를 발동한다. 2개 이상 넣으면 보스 다중 스킬.\n" +
                 "쿨다운·사거리·데미지·CC 는 전부 Ability SO 에서 편집한다(여기 중복 저작 없음).")]
        public List<string> abilityIds = new();

        [Header("보상")]
        [Tooltip("처치 시 획득 경험치(Main 킬 보상). 던전은 맵 클리어 단위라 미사용 가능.")]
        public int expReward = 20;

        [Tooltip("등급 분류(AC-G). 배율이 아니다 — 변종은 각자 ID·스탯을 직접 저작한다. " +
                 "표시·연출 분기용이고 스탯에 곱해지지 않는다. " +
                 "보스를 만들려면: 이 행을 복제해 monsterId 를 leviathan_boss 로 바꾸고 maxHp 를 직접 올린다.")]
        public MonsterTierId tier = MonsterTierId.Normal;
    }
}
