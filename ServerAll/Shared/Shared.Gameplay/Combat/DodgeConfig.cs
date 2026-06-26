namespace Script.System.GamePlayAbilitySystem
{
    /// <summary>
    /// 회피(Dodge)의 <b>게임플레이 권위 수치</b>(클라·서버 공유 단일 소스).
    ///
    /// 무적 창(IframeMs)·쿨다운(CooldownMs)은 서버가 진실원이다 — 던전(서버 권위)에서 클라가 C_Dodge 를
    /// 연사해 영구 무적을 만드는 치팅을 막으려면 서버가 같은 상수로 쿨다운을 강제해야 한다.
    /// 클라는 동일 수치로 로컬 무적 태그/연출을 맞춰 예측한다.
    ///
    /// 대시 속도/거리(연출·이동 감각)는 클라 전용(LocomotionSettings) — 서버 데미지 판정과 무관해 공유 안 함.
    /// </summary>
    public static class DodgeConfig
    {
        /// <summary>무적 프레임 지속(ms). 회피 발동 후 이 시간 동안 피해 무시.</summary>
        public const int IframeMs = 500;

        /// <summary>회피 쿨다운(ms). 직전 발동에서 이 시간이 지나야 다시 회피 가능(서버 권위 게이트).</summary>
        public const int CooldownMs = 1000;

        /// <summary>회피 마나 코스트(클라 게이트 / 서버 검증·차감). 0 = 무료.</summary>
        public const int ManaCost = 15;
    }
}
