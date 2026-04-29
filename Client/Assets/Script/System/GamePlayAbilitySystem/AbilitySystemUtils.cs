namespace Script.System.GamePlayAbilitySystem
{
    public static class AbilitySystemUtils
    {
        public static void ApplyEffect(AbilitySystemComponent target, GameplayEffect effect)
        {
            effect.ApplyEffect(target);
        }
    }
}