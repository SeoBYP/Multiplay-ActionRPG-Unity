using Script.System.GamePlayAbilitySystem;
using Shared.Infrastructure.Consumables;

namespace Server.Combat;

/// <summary>
/// 서버 권위 전투에서 effectId → Attribute 모디파이어 해석(GAS)의 **서버측 얇은 접근자**.
/// 몬스터 HP(서버 권위) 차감 시 GameplayEffectMath 로 집계할 모디파이어를 제공한다.
///
/// 수치 진실원(이원화):
///   - 전투 효과(basic_attack_dmg/monster_attack_dmg 등) = 코드 시드 `GameplayEffectCatalog`(서버 게임밸런스 권위).
///   - 소모품 회복(potion_*) = 클라 `ConsumableCatalog` SO 저작 → bake → `ConsumableEffectCatalog`(임베디드 JSON).
///     static ctor 가 그 정의를 같은 `Register` API 로 흡수해 effectId 단일 조회로 합류시킨다(GameplayEffectCatalog 의 "2단계 JSON 로더").
/// ※ 플레이어 HP 는 클라가 같은 카탈로그로 결정론 계산(여기 미사용, 몬스터 HP·서버 회복 적용 전용).
/// </summary>
public static class CombatEffectCatalog
{
    // 수치 진실원: 코드 시드(전투) + 임베디드 JSON(소모품). 서버는 그 정의의 Instant 모디파이어만 꺼내 쓴다.
    private static readonly GameplayEffectCatalog _shared = new();

    static CombatEffectCatalog()
    {
        // 소모품 회복 = 클라 SO 에서 bake 된 JSON 을 같은 카탈로그에 흡수(effectId == itemId).
        foreach (var def in ConsumableEffectCatalog.LoadDefinitions())
            _shared.Register(def);
    }

    /// <summary>effectId 의 Attribute 모디파이어 목록(Shared 카탈로그 위임). 미등록이면 빈 목록.</summary>
    public static IReadOnlyList<GameplayAttributeModifier> Resolve(string effectId)
        => _shared.Get(effectId)?.Modifiers ?? Array.Empty<GameplayAttributeModifier>();
}
