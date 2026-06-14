using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Progression
{
    /// <summary>
    /// 레벨별 진행 데이터 저작(authoring) 진실원. 디자이너가 이 SO를 Inspector 에서 편집한다.
    /// (DropTableDefinition·MapDefinition 과 동일 컨벤션 — SO 저작 → JSON bake → 서버 임베디드.)
    ///
    /// 한 행 = 레벨 L 의 { 다음 레벨까지 Exp + 스탯 }. 스탯은 레벨에서 결정론 룩업 —
    /// DB 에는 Level/Exp 만 영속하고 스탯은 항상 이 테이블에서 파생한다(단일소스, 2.4 스탯합산의 base).
    ///
    /// - 클라(스탯창)는 이 SO를 직접 읽는다(Resources.Load).
    /// - 서버(레벨업 루프·gRPC 스탯 응답)는 UnityEngine 의존이 0이라 SO를 못 읽으므로, Export 툴
    ///   (Tools/Progression/Export)이 이 SO를 level-table.json 으로 bake → 서버 임베디드 파싱
    ///   (Shared.Infrastructure.Progression.LevelTable).
    ///
    /// 60행 시드는 Tools/Progression/Generate Default Curve(거듭제곱 Exp + 선형 스탯)로 채운 뒤 Inspector 에서 조정한다.
    /// </summary>
    [CreateAssetMenu(fileName = "LevelTableDefinition", menuName = "Game/Level Table Definition", order = 3)]
    public sealed class LevelTableDefinition : ScriptableObject
    {
        [Tooltip("레벨별 진행 행. level 은 1부터 연속이어야 한다(최고 레벨 = rows 의 최대 level).")]
        public List<LevelRowDef> rows = new();
    }

    /// <summary>레벨 1개의 진행 데이터 — 다음 레벨까지 Exp + base 스탯.</summary>
    [Serializable]
    public sealed class LevelRowDef
    {
        public int level = 1;

        [Tooltip("이 레벨에서 다음 레벨로 가는 데 필요한 Exp. 최고 레벨은 0(다음 없음).")]
        public long expToNext;

        public int maxHealth = 100;
        public int maxMana = 50;
        public int attackPower = 10;
        public int defense = 5;
        public int strength = 5;
        public int dexterity = 5;
        public int intelligence = 5;
    }
}
