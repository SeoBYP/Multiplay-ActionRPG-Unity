using System.Collections.Generic;

namespace Script.System.GamePlayAbilitySystem
{
    /// <summary>
    /// EffectId → 정의 조회. 1단계는 코드로 시드 등록한다 (ScriptableObject/JSON을 진실원으로 쓰지 않음 — plan 준수).
    /// 2단계에서 공유 JSON 로더가 같은 Register API로 정의를 채운다.
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
        }
    }
}
