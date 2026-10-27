using Script.System.GamePlayAbilitySystem;
using Shared.Infrastructure.Items;

namespace Server.Combat;

/// <summary>
/// effectId → GameplayEffect 정의의 서버측 얇은 접근자. 몬스터 HP 차감·서버 회복 적용 전용
/// (플레이어 HP 는 클라가 같은 카탈로그로 결정론 계산).
///
/// 수치 진실원은 둘 — 전투 효과 = 코드 시드 `GameplayEffectCatalog`,
/// 소모품 회복(potion_*) = 임베디드 items.json 의 consumeEffects. static ctor 가 후자를 같은 카탈로그에 흡수해 effectId 단일 조회로 합친다.
/// </summary>
public static class CombatEffectCatalog
{
    private static readonly GameplayEffectCatalog _shared = new();

    static CombatEffectCatalog()
    {
        // effectId == itemId
        foreach (var def in ItemCatalogData.Current.Consumables)
            _shared.Register(def);
    }

    /// <summary>effectId 의 Attribute 모디파이어 목록(Shared 카탈로그 위임). 미등록이면 빈 목록.</summary>
    public static IReadOnlyList<GameplayAttributeModifier> Resolve(string effectId)
        => _shared.Get(effectId)?.Modifiers ?? Array.Empty<GameplayAttributeModifier>();

    /// <summary>
    /// effectId 의 <b>정의 전체</b>. 미등록이면 null.
    /// 지속효과는 모디파이어만으로 표현되지 않는다 — 지속시간·스택 정책·GrantedTags 가 있어야
    /// 서버가 만료를 소유할 수 있다(<see cref="AbilitySystemComponent.ApplyEffect"/>).
    /// </summary>
    public static GameplayEffectDefinition? Get(string effectId) => _shared.Get(effectId);
}
