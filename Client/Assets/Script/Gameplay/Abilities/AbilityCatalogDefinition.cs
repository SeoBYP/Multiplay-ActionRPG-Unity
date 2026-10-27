using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Abilities
{
    /// <summary>
    /// 어빌리티 자산(<see cref="AbilityDefinition"/>) 묶음 — 플레이어 스킬·몬스터 공격 전부 여기 등록한다.
    /// 런타임(클라 조회)·Export bake 가 모두 이 목록을 보므로 드리프트가 없다.
    /// </summary>
    [CreateAssetMenu(fileName = "AbilityCatalogDefinition", menuName = "Game/Ability Catalog Definition", order = 6)]
    public sealed class AbilityCatalogDefinition : ScriptableObject
    {
        [Tooltip("어빌리티 자산 목록. 각 항목은 별도 Ability_*.asset 참조.")]
        public List<AbilityDefinition> abilities = new();

        /// <summary>문자열 id 로 조회(저작·디버그용). 미등록이면 null.</summary>
        public AbilityDefinition Get(string id)
        {
            foreach (var a in abilities)
                if (a != null && a.id == id)
                    return a;
            return null;
        }

        /// <summary>패킷 networkId 로 조회(클라 Cue 라우팅 — S_AbilityActivated.SkillId). 미등록이면 null.</summary>
        public AbilityDefinition GetByNetworkId(int networkId)
        {
            foreach (var a in abilities)
                if (a != null && a.networkId == networkId)
                    return a;
            return null;
        }
    }
}
