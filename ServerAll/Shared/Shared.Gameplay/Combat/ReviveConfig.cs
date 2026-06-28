namespace Script.System.GamePlayAbilitySystem
{
    /// <summary>
    /// Co-op 부활(2.5.2)의 <b>게임플레이 권위 수치</b>(클라·서버 공유 단일 소스, DodgeConfig 형제).
    ///
    /// 던전(서버 권위)에서 다운된 아군을 살리는 상호작용. 거리·부활량은 서버가 진실원 —
    /// 클라가 C_Revive 를 위조해도 서버가 같은 상수로 거리/상태를 재검증한다.
    /// 홀드 시간은 클라 UX 게이트(시전 채널) — 서버는 거리/다운상태/미실패만 검증한다(사용자 결정).
    /// </summary>
    public static class ReviveConfig
    {
        /// <summary>부활 가능 거리(m, 평면). 시전자와 다운 아군이 이 안이어야 한다(서버 권위 검증).</summary>
        public const float RangeMeters = 2.5f;

        /// <summary>부활 시전(홀드) 시간(초). 클라가 제자리 유지로 채널을 채우면 C_Revive 송신(클라 UX).</summary>
        public const float HoldSeconds = 3f;

        /// <summary>부활 시 복구되는 HP 비율(MaxHp 대비 %). 서버 권위로 적용·브로드캐스트.</summary>
        public const int RestorePercent = 50;
    }
}
