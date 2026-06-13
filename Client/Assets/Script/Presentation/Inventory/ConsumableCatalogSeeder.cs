using System.Collections.Generic;
using Script.System.GamePlayAbilitySystem;
using VContainer.Unity;

namespace Game.Presentation.Inventory
{
    /// <summary>
    /// 소모품 회복 effect 를 클라 DI `GameplayEffectCatalog` 에 등록한다(`ConsumableCatalog` SO → 카탈로그).
    ///
    /// 데이터 진실원 교리(gas-architecture §2.5): 소모품 수치 저작 = 클라 SO. 서버는 그 SO 를 bake 한 JSON 을 읽고,
    /// 클라는 이 Seeder 로 같은 SO 를 런타임 카탈로그에 흡수한다 → effectId(==itemId) 단일 조회로 양쪽 일치.
    ///
    /// 왜 필요: 전투 effect 만 코드 시드라 GameplayEffectCatalog 에 potion_* 이 없다. 이 Seeder 가 없으면
    /// 던전 회복 미러(`EffectReceiver` 가 S_ApplyEffect(potion_hp_small) 수신 → Get(effectId))가 null → 회복 안 보임.
    /// </summary>
    public sealed class ConsumableCatalogSeeder : IInitializable
    {
        private readonly ConsumableCatalog    _catalog;
        private readonly GameplayEffectCatalog _effectCatalog;

        public ConsumableCatalogSeeder(ConsumableCatalog catalog, GameplayEffectCatalog effectCatalog)
        {
            _catalog       = catalog;
            _effectCatalog = effectCatalog;
        }

        public void Initialize()
        {
            if (_catalog?.items == null || _effectCatalog == null)
                return;

            foreach (var item in _catalog.items)
            {
                if (item == null || string.IsNullOrEmpty(item.itemId) || item.effects == null)
                    continue;

                // 소모품 = 즉발 회복(서버 ConsumableEffectCatalog 와 동일하게 Instant 로 흡수). 다중 효과는 mod 로 평탄화.
                var mods = new List<GameplayAttributeModifier>(item.effects.Count);
                foreach (var e in item.effects)
                    mods.Add(GameplayAttributeModifier.Create(e.stat, e.amount, EModifierType.Additive));

                _effectCatalog.Register(new GameplayEffectDefinition(
                    id: item.itemId,
                    category: EEffectCategory.AttackPower, // Instant 회복 = 버프 아이콘 없음(cosmetic)
                    policy: EDurationPolicy.Instant,
                    durationMs: 0,
                    modifiers: mods));
            }
        }
    }
}
