using Script.System.GamePlayAbilitySystem;
using Shared.Infrastructure.Items;

namespace Server.Combat;

// 수치 진실원 둘 — 전투 효과 = GameplayEffectCatalog 시드, 소모품 회복 = items.json consumeEffects(static ctor 가 흡수).
// 플레이어 HP 는 클라가 같은 카탈로그로 결정론 계산한다.
public static class CombatEffectCatalog
{
    private static readonly GameplayEffectCatalog _shared = new();

    static CombatEffectCatalog()
    {
        // effectId == itemId
        foreach (var def in ItemCatalogData.Current.Consumables)
            _shared.Register(def);
    }

    public static IReadOnlyList<GameplayAttributeModifier> Resolve(string effectId)
        => _shared.Get(effectId)?.Modifiers ?? Array.Empty<GameplayAttributeModifier>();

    // 서버가 만료를 소유하려면 모디파이어만으론 부족하다 — 지속시간·스택 정책·GrantedTags 필요. 미등록 null.
    public static GameplayEffectDefinition? Get(string effectId) => _shared.Get(effectId);
}
