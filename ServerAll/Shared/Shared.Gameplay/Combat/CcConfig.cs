namespace Script.System.GamePlayAbilitySystem
{
    /// <summary>
    /// 상태이상(CC)의 <b>게임플레이 권위 수치</b>(클라·서버 공유 단일 소스).
    ///
    /// 지속시간(스턴/슬로우)은 각 효과 정의(<see cref="GameplayEffectDefinition.DurationMs"/>)가 소유하고,
    /// 여기엔 효과 정의로 표현하기 애매한 "감속 배율"만 둔다(슬로우 태그엔 크기가 없으므로 게이트가 이 상수로 곱한다).
    /// 더 세분화된 슬로우 단계가 필요해지면 MoveSpeed 를 GameplayAttribute 로 승격(YAGNI — 지금은 단일 배율).
    /// </summary>
    public static class CcConfig
    {
        /// <summary>슬로우 적용 시 이동 속도 배율(0~1). 0.5 = 절반 속도.</summary>
        public const float SlowMultiplier = 0.5f;
    }
}
