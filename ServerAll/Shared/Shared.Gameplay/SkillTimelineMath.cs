namespace Script.System.GamePlayAbilitySystem
{
    /// <summary>
    /// 스킬 페이즈/active window 판정의 순수 함수. 서버·클라가 동일 결과(결정론).
    /// </summary>
    public static class SkillTimelineMath
    {
        public static ESkillPhase PhaseAt(SkillTimeline timeline, int elapsedMs)
        {
            if (elapsedMs < timeline.ActiveStartMs) return ESkillPhase.Startup;
            if (elapsedMs < timeline.ActiveEndMs) return ESkillPhase.Active;
            if (elapsedMs < timeline.TotalMs) return ESkillPhase.Recovery;
            return ESkillPhase.Done;
        }

        /// <summary>active window [ActiveStart, ActiveEnd) 안인지. 이 구간에서만 hitbox를 평가한다.</summary>
        public static bool IsActive(SkillTimeline timeline, int elapsedMs)
            => elapsedMs >= timeline.ActiveStartMs && elapsedMs < timeline.ActiveEndMs;
    }
}
