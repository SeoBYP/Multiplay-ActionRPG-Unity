using Script.System.GamePlayAbilitySystem;

namespace Server.Combat;

/// <summary>
/// 서버 권위 전투에서 effectId → Attribute 모디파이어 해석(GAS).
/// 몬스터 HP(서버 권위)에 데미지를 적용할 때 GameplayEffectMath 로 집계할 모디파이어를 제공한다.
///
/// ※ 플레이어 HP 는 기존대로 클라가 공유 GameplayEffectCatalog 로 결정론 계산한다(여기 미사용).
/// ⚠ magnitude 단일소스 통합(클라 GameplayEffectCatalog 와)은 후속 과제 — 지금은 서버 몬스터 HP 전용.
/// </summary>
public static class CombatEffectCatalog
{
    private static readonly Dictionary<string, GameplayAttributeModifier[]> Effects = new()
    {
        ["basic_attack_dmg"] = new[]
        {
            new GameplayAttributeModifier(EGameplayAttribute.Health, -10, EModifierType.Additive),
        },
    };

    /// <summary>effectId 의 Attribute 모디파이어 목록. 미등록이면 빈 목록.</summary>
    public static IReadOnlyList<GameplayAttributeModifier> Resolve(string effectId)
        => effectId != null && Effects.TryGetValue(effectId, out var mods)
            ? mods
            : Array.Empty<GameplayAttributeModifier>();
}
