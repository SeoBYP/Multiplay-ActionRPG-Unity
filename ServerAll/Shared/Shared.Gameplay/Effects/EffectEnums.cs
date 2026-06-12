namespace Script.System.GamePlayAbilitySystem
{
    /// <summary>버프/디버프 아이콘 매칭 키.</summary>
    public enum EEffectCategory
    {
        AttackPower,
        Defense,
        MoveSpeed,
    }

    public enum EDurationPolicy
    {
        Instant,
        Duration,
        Infinite,
    }

    public enum EStackPolicy
    {
        None,
        Refresh,
        Stack,
    }
}
