namespace Script.System.GamePlayAbilitySystem
{
    /// <summary>GAS 호출을 모아두는 얇은 유틸리티 계층.</summary>
    public static class AbilitySystemUtils
    {
        public static void ApplyEffect(GasComponent target, GameplayEffect effect)
        {
            effect.ApplyEffect(target);
        }
    }
}
