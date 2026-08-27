namespace Script.System.GamePlayAbilitySystem
{
    /// <summary>
    /// GAS 호출을 모아두는 얇은 유틸리티 계층이다.
    /// 이후 서버 권위, 로깅, 태그 검사 같은 공통 처리를 이 지점에 넣을 수 있다.
    /// </summary>
    public static class AbilitySystemUtils
    {
        public static void ApplyEffect(GasComponent target, GameplayEffect effect)
        {
            effect.ApplyEffect(target);
        }
    }
}
