using System.Collections.Generic;
using Script.System.GamePlayAbilitySystem;

namespace Game.Gameplay.Abilities
{
    /// <summary>
    /// 클라 런타임 스킬 조회 — <see cref="SkillCatalogDefinition"/>(SO) 를 id→<see cref="SkillTimeline"/> 맵으로 펼친다.
    /// 소비자: <c>LocalCombat</c>(Main hitbox) 등. 서버는 같은 데이터를 bake JSON 으로 읽는다(`Shared.Infrastructure.Skills.SkillCatalog`).
    /// VContainer 단일 인스턴스로 등록(LifetimeScope 가 카탈로그 SO 로드 후 생성).
    /// </summary>
    public sealed class SkillCatalogProvider
    {
        private readonly Dictionary<string, SkillTimeline> _map = new();

        public SkillCatalogProvider(SkillCatalogDefinition catalog)
        {
            if (catalog == null) return;
            foreach (var s in catalog.skills)
                if (s != null && !string.IsNullOrEmpty(s.id))
                    _map[s.id] = s.ToTimeline();
        }

        /// <summary>id 의 타임라인. 미등록이면 null.</summary>
        public SkillTimeline Get(string id)
            => id != null && _map.TryGetValue(id, out var t) ? t : null;
    }
}
