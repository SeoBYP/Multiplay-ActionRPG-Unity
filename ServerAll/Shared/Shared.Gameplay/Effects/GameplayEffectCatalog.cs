using System.Collections.Generic;

namespace Script.System.GamePlayAbilitySystem
{
    /// <summary>
    /// EffectId → 정의 조회. **전투 밸런스 effect**(basic_attack_dmg/monster_attack_dmg 등)는 서버 권위라 여기 코드 시드로 둔다.
    /// **콘텐츠 effect**(소모품 회복 등)는 클라 SO 저작 → bake JSON → `Register` API 로 흡수한다(서버 측은 `CombatEffectCatalog` static ctor).
    /// 데이터 진실원 교리 = gas-architecture.md §2.5 (SO 저작/Shared 배포·검증).
    /// </summary>
    public sealed class GameplayEffectCatalog
    {
        private readonly Dictionary<string, GameplayEffectDefinition> _defs = new();

        public GameplayEffectCatalog()
        {
            SeedDefaults();
        }

        public void Register(GameplayEffectDefinition def)
        {
            if (def != null)
                _defs[def.Id] = def;
        }

        public GameplayEffectDefinition Get(string id)
        {
            return id != null && _defs.TryGetValue(id, out var def) ? def : null;
        }

        public bool TryGet(string id, out GameplayEffectDefinition def)
        {
            def = null;
            return id != null && _defs.TryGetValue(id, out def);
        }

        // 1단계 시드 — 추후 JSON으로 대체.
        private void SeedDefaults()
        {
            Register(new GameplayEffectDefinition(
                id: "atk_up_20",
                category: EEffectCategory.AttackPower,
                policy: EDurationPolicy.Duration,
                durationMs: 10000,
                modifiers: new[]
                {
                    GameplayAttributeModifier.Create(EGameplayAttribute.AttackPower, 120, EModifierType.Multiplicative),
                },
                stack: EStackPolicy.Refresh));

            Register(new GameplayEffectDefinition(
                id: "def_down_10",
                category: EEffectCategory.Defense,
                policy: EDurationPolicy.Duration,
                durationMs: 8000,
                modifiers: new[]
                {
                    GameplayAttributeModifier.Create(EGameplayAttribute.Defense, -10, EModifierType.Additive),
                }));

            // AC-B 안B: **데미지 단일 라벨**. 값은 여기서 정하지 않는다 — 서버가 `ability.baseDamage` 로 계산해
            // S_ApplyEffect.Amount(권위 델타)로 실어 보내고, 클라 ApplyEffectAuthoritative 의 healthOverride 가 이 값을 덮어쓴다.
            // 즉 이 정의는 "즉발 Health 피해"라는 **형태**만 제공하고 수치는 어빌리티가 소유한다(폐기된 basic_attack_dmg/combo_*_dmg/monster_attack_dmg 대체).
            Register(new GameplayEffectDefinition(
                id: "ability_damage",
                category: EEffectCategory.AttackPower,
                policy: EDurationPolicy.Instant,
                durationMs: 0,
                modifiers: new[]
                {
                    GameplayAttributeModifier.Create(EGameplayAttribute.Health, -1, EModifierType.Additive), // placeholder — Amount 가 항상 덮어씀
                }));

            // 2.6.2 상태이상(CC) — Duration 효과가 GrantedTags 로 상태 태그를 부여한다(modifier 없음).
            //   HasTag 가 활성 효과 GrantedTags 를 동적 합산 → 게이트(입력/이동)가 폴링 → Tick 자동 만료.
            //   부여 경로: 던전=서버 S_ApplyEffect(EffectId) → EffectReceiver / Main=LocalMonster 로컬 적용. 새 패킷 없음.
            Register(new GameplayEffectDefinition(
                id: "stun_1_5s",
                category: EEffectCategory.Defense, // 디버프(표시 색 판정용). modifier 없음 = 순수 상태태그.
                policy: EDurationPolicy.Duration,
                durationMs: 1500,
                modifiers: new List<GameplayAttributeModifier>(),
                stack: EStackPolicy.Refresh,
                grantedTags: new GameplayTag[] { GameplayTags.Stun }));

            Register(new GameplayEffectDefinition(
                id: "slow_3s",
                category: EEffectCategory.Defense,
                policy: EDurationPolicy.Duration,
                durationMs: 3000,
                modifiers: new List<GameplayAttributeModifier>(),
                stack: EStackPolicy.Refresh,
                grantedTags: new GameplayTag[] { GameplayTags.Slow }));

            // 소모품 회복(potion_*)은 이 코드 시드에 두지 않는다 — 단일소스 = 클라 `ConsumableCatalog` SO.
            //   서버: Export 툴이 bake 한 임베디드 JSON(`ConsumableEffectCatalog`)을 `CombatEffectCatalog` static ctor 가 Register 로 흡수.
            //   클라: `ConsumableCatalogSeeder` 가 같은 SO 를 이 카탈로그에 Register(던전 EffectReceiver 미러용).
            // → effectId == itemId 규칙으로 양쪽 동일 수치. (이 주석이 GameplayEffectCatalog "2단계 JSON 로더"의 실현)
        }
    }
}
