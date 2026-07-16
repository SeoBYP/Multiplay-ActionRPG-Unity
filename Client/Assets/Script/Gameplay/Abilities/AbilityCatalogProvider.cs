using System.Collections.Generic;
using Script.System.GamePlayAbilitySystem;

namespace Game.Gameplay.Abilities
{
    /// <summary>
    /// 클라 런타임 어빌리티 조회 — <see cref="AbilityCatalogDefinition"/>(SO) 를 id / networkId 맵으로 펼친다(AC-B).
    /// 서버는 **같은 저작 데이터**를 bake JSON 으로 읽는다(`Shared.Infrastructure.Abilities.AbilityCatalog`) → 드리프트 없음.
    ///
    /// 소비자:
    ///   · 게임플레이 — `LocalCombat`(Main hitbox) · `PlayerCharacterAgent`(쿨다운·마나 예측): <see cref="GetTimeline"/>
    ///   · 연출 — `AbilityCueRouter`(네트워크 발동 신호 → Cue): <see cref="Get(int)"/> 의 cueTrigger/cueComboStep
    /// VContainer 단일 인스턴스로 등록(LifetimeScope 가 카탈로그 SO 로드 후 생성).
    ///
    /// ※ 구 <c>SkillCatalogProvider</c> 대체 — int→문자열 하드코딩 매핑(SkillName switch)이 사라지고
    ///   networkId 가 **데이터**(AbilityDefinition.networkId)로 조회된다.
    /// </summary>
    public sealed class AbilityCatalogProvider
    {
        private readonly Dictionary<string, AbilityDefinition> _byId = new();
        private readonly Dictionary<int, AbilityDefinition> _byNetworkId = new();
        private readonly Dictionary<string, SkillTimeline> _timelines = new();

        public AbilityCatalogProvider(AbilityCatalogDefinition catalog)
        {
            if (catalog == null) return;
            foreach (var a in catalog.abilities)
            {
                if (a == null || string.IsNullOrEmpty(a.id)) continue;
                _byId[a.id] = a;
                _byNetworkId[a.networkId] = a;
                _timelines[a.id] = a.ToTimeline();
            }
        }

        /// <summary>문자열 id 의 어빌리티. 미등록이면 null.</summary>
        public AbilityDefinition Get(string id)
            => id != null && _byId.TryGetValue(id, out var a) ? a : null;

        /// <summary>패킷 networkId(C_Attack/S_AbilityActivated 의 SkillId)의 어빌리티. 미등록이면 null.</summary>
        public AbilityDefinition Get(int networkId)
            => _byNetworkId.TryGetValue(networkId, out var a) ? a : null;

        /// <summary>게임플레이 판정 데이터(hitbox·쿨다운·on-hit). 미등록이면 null.</summary>
        public SkillTimeline GetTimeline(string id)
            => id != null && _timelines.TryGetValue(id, out var t) ? t : null;

        /// <summary>networkId 로 게임플레이 판정 데이터 조회(구 SkillName switch 대체). 미등록이면 null.</summary>
        public SkillTimeline GetTimeline(int networkId)
            => Get(networkId) is { } a ? GetTimeline(a.id) : null;
    }
}
