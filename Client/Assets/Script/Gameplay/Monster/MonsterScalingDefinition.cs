using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Monster
{
    /// <summary>
    /// 몬스터 등급(저작용). 서버 <c>Shared.Infrastructure.Monsters.MonsterTier</c> 의 **미러**다 —
    /// 그쪽은 서버 전용 어셈블리라 클라가 참조할 수 없다. 계약은 <b>JSON 의 int</b>(0/1/2).
    /// <para>값을 추가하면 <b>양쪽을 같이</b> 고쳐야 한다(리뷰 대상).</para>
    /// </summary>
    public enum MonsterTierId
    {
        Normal = 0,
        Elite = 1,
        Boss = 2,
    }

    /// <summary>한 등급의 배율 행.</summary>
    [Serializable]
    public sealed class MonsterTierRow
    {
        public MonsterTierId tier = MonsterTierId.Normal;

        [Tooltip("최대 HP 배율. 등급의 주력 축 — 크게 올린다.")]
        public float hpMultiplier = 1f;

        [Tooltip("피해 배율. 작게 올린다 — 크게 올리면 즉사가 된다.")]
        public float damageMultiplier = 1f;

        [Tooltip("경험치 배율.")]
        public float expMultiplier = 1f;

        [Tooltip("드롭 확률 배율(수량 아님). 결과 확률은 서버가 1.0 으로 clamp 한다.")]
        public float dropChanceMultiplier = 1f;
    }

    /// <summary>
    /// 몬스터 등급 배율 테이블(AC-F2 저작). <c>Tools/Monster/Export Monster Scaling</c> 으로
    /// <c>monster-scaling.json</c> 에 bake 되어 양 서버가 읽는다.
    ///
    /// <para><b>왜 SO 인가</b>: 이 배율들이 서버 코드에 <c>switch</c> 로 박혀 있어 기획이 값을 바꾸려면
    /// 코드를 고쳐야 했다. 다른 밸런스 데이터(MonsterCatalog·DropTable·LevelTable)와 같은 교리로 옮긴다 —
    /// <b>수치는 코드가 아니라 테이블에 있다.</b></para>
    ///
    /// <para><b>레벨 곡선은 여기 없다</b> — 몬스터 스케일은 <c>LevelTableDefinition</c>(플레이어 곡선)을
    /// 서버가 직접 읽어 유도한다. 곡선을 여기 복제하면 두 테이블이 어긋난다.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "MonsterScalingDefinition", menuName = "Game/Monster Scaling Definition", order = 3)]
    public sealed class MonsterScalingDefinition : ScriptableObject
    {
        [Tooltip("등급별 배율. 등급 하나당 한 행. 원칙: HP 를 크게, 피해를 작게.")]
        public List<MonsterTierRow> tiers = new();
    }
}
