namespace Script.System.GamePlayAbilitySystem
{
    /// <summary>
    /// well-known GameplayTag 문자열 상수. 매직 스트링·오타 방지(클라·서버 공유).
    /// 새 상태 태그는 여기에 모은다(예: 스턴·무적).
    /// </summary>
    public static class GameplayTags
    {
        /// <summary>사망(다운). 입력 게이트가 폴링해 이동/공격/상호작용을 억제한다.</summary>
        public const string Dead = "State.Dead";

        /// <summary>회피(Dodge) 무적 프레임. 켜져 있는 동안 피해를 무시한다(Main 클라권위 / 던전 서버권위 양쪽 게이트).</summary>
        public const string Invulnerable = "State.Invulnerable";

        /// <summary>스턴(CC). 활성 동안 입력/이동을 정지한다. Duration 효과의 GrantedTags 로 부여→자동 만료.</summary>
        public const string Stun = "State.Stun";

        /// <summary>슬로우(CC). 활성 동안 이동 속도를 CcConfig.SlowMultiplier 배로 감속. Duration 효과의 GrantedTags 로 부여.</summary>
        public const string Slow = "State.Slow";
    }
}
