using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Abilities
{
    /// <summary>
    /// 스킬 자산(<see cref="SkillDefinition"/>) 묶음. 기획자가 스킬당 자산을 만들어 이 리스트에 등록한다.
    /// 단일 진입점 — 런타임(Addressables 로드)·Export bake 가 모두 이 목록을 본다(드리프트 방지).
    /// (MonsterCatalogDefinition 컨벤션이되, 스킬은 자산을 분리해 참조로 담는다 = "각 스킬별 Asset".)
    /// </summary>
    [CreateAssetMenu(fileName = "SkillCatalogDefinition", menuName = "Game/Skill Catalog Definition", order = 6)]
    public sealed class SkillCatalogDefinition : ScriptableObject
    {
        [Tooltip("스킬 자산 목록. 각 항목은 별도 SkillDefinition.asset 참조.")]
        public List<SkillDefinition> skills = new();

        public SkillDefinition Get(string id)
        {
            foreach (var s in skills)
                if (s != null && s.id == id)
                    return s;
            return null;
        }
    }
}
