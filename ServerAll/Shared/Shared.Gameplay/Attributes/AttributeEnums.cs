
namespace Script.System.GamePlayAbilitySystem
{
    /// <summary>Resource: 즉발로 영구 변동(HP/MP). Stat: 활성 Effect로부터 파생(공격력 등).</summary>
    public enum EAttributeKind
    {
        Resource,
        Stat,
    }

    public enum EGameplayAttribute
    {
        // Resource
        Health,
        Stamina,
        Mana,
        // 1차 스탯
        Strength,
        Dexterity,
        Intelligence,
        // Stat
        AttackPower,
        Defense,
        MoveSpeed,
    }

    public enum EModifierType
    {
        Additive,
        Multiplicative,
    }
}
