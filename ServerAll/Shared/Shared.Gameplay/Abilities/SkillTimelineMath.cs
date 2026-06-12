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

        /// <summary>
        /// 마지막 발동(lastCastMs) 이후 cooldownMs가 지났는지. 서버 발동 게이트(연사=폭딜 치팅 차단)용.
        /// 첫 발동(lastCastMs=0 등 과거값)은 항상 true.
        /// </summary>
        public static bool CooldownElapsed(int cooldownMs, long lastCastMs, long nowMs)
            => nowMs - lastCastMs >= cooldownMs;
    }
}
