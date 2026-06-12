using Script.System.GamePlayAbilitySystem;

namespace Server.Combat;

/// <summary>
/// 서버 권위 전투에서 effectId → Attribute 모디파이어 해석(GAS)의 **서버측 얇은 접근자**.
/// 몬스터 HP(서버 권위) 차감 시 GameplayEffectMath 로 집계할 모디파이어를 제공한다.
///
/// 수치 **단일소스 = Shared `GameplayEffectCatalog`**(클라·서버 공유, ns 동일). 예전엔 자체
/// Dictionary 로 수치를 중복 정의했으나(클라 카탈로그와 이중정의·드리프트 위험) 단일소스로 위임했다.
/// ※ 플레이어 HP 는 기존대로 클라가 같은 카탈로그로 결정론 계산(여기 미사용, 몬스터 HP 전용).
/// </summary>
public static class CombatEffectCatalog
{
    // 수치 진실원은 Shared 카탈로그 하나. 서버는 그 정의의 Instant 모디파이어만 꺼내 쓴다.
    private static readonly GameplayEffectCatalog _shared = new();

    /// <summary>effectId 의 Attribute 모디파이어 목록(Shared 카탈로그 위임). 미등록이면 빈 목록.</summary>
    public static IReadOnlyList<GameplayAttributeModifier> Resolve(string effectId)
        => _shared.Get(effectId)?.Modifiers ?? Array.Empty<GameplayAttributeModifier>();
}
