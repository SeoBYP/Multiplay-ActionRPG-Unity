using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Loot
{
    /// <summary>
    /// 드랍 테이블 저작(authoring) 진실원. 디자이너가 이 SO를 Inspector 에서 편집한다.
    /// (실무는 Excel, 개인 프로젝트는 SO로 간편화 — MapDefinition 과 동일 컨벤션.)
    ///
    /// - **저작 전용 SO**(런타임 미로드). 클라·서버 모두 bake된 drop-tables.json(Shared.Infrastructure.DropTableCatalog)을 읽는다.
    ///   → 이 SO는 Resources 밖(Assets/GameData/Loot)이며 빌드에 포함되지 않는다.
    /// - 서버(던전 드랍)는 UnityEngine 의존이 0이라 SO를 못 읽으므로, Export 툴
    ///   (Tools/Loot/Export Drop Tables)이 이 SO를 drop-tables.json 으로 bake → 서버 임베디드 파싱
    ///   (Shared.Infrastructure.DropTableCatalog). roll 로직은 양쪽 Shared.Gameplay.DropTableRoll 공유.
    /// </summary>
    [CreateAssetMenu(fileName = "DropTableDefinition", menuName = "Game/Drop Table Definition", order = 1)]
    public sealed class DropTableDefinition : ScriptableObject
    {
        [Tooltip("몬스터 타입별 드랍 후보. monsterId 는 서버/클라 공용 식별자(예: slime).")]
        public List<MonsterDropTable> tables = new();

        /// <summary>monsterId 의 드랍 후보를 반환한다. 미등록이면 빈 목록. (클라 런타임 조회용)</summary>
        public IReadOnlyList<DropEntryDef> Get(string monsterId)
        {
            foreach (var t in tables)
                if (t.monsterId == monsterId)
                    return t.drops;
            return Array.Empty<DropEntryDef>();
        }
    }

    /// <summary>한 몬스터 타입의 드랍 후보 목록.</summary>
    [Serializable]
    public sealed class MonsterDropTable
    {
        [Tooltip("몬스터 타입 키(서버·클라 공용, GameServer ItemCatalog 와 문자열 정렬).")]
        public string monsterId;
        public List<DropEntryDef> drops = new();
    }

    /// <summary>드랍 후보 1개 — itemId 가 chance 확률로 [minQty, maxQty] 수량 떨어진다.</summary>
    [Serializable]
    public sealed class DropEntryDef
    {
        public string itemId;
        [Range(0f, 1f)] public float chance = 1f;
        public int minQty = 1;
        public int maxQty = 1;
    }
}
