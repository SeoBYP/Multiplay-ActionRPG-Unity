namespace Script.System.GamePlayAbilitySystem
{
    /// <summary>어빌리티 발동 게이트 판정 결과. Ok 가 아니면 발동하지 않는다.</summary>
    public enum AbilityActivationResult
    {
        Ok,
        Blocked,        // 차단 태그(사망·스턴 등) — 발동 자체 불가
        OnCooldown,     // 쿨다운 미경과
        NotEnoughMana,  // 마나 부족
    }

    /// <summary>
    /// 어빌리티 발동 게이트의 <b>순수 함수</b>(무상태·엔진 비의존 → 클라 예측과 서버 권위가 같은 함수를 호출).
    /// 원시 파라미터를 받는 이유: 플레이어(<see cref="SkillTimeline"/>)와 몬스터(MonsterDef)가 같은 게이트를 쓰되 각자 데이터에서 값을 먹인다.
    /// 우선순위: Blocked → OnCooldown → NotEnoughMana → Ok (죽었으면 쿨다운·마나와 무관하게 못 쓴다).
    /// </summary>
    public static class AbilityActivationMath
    {
        /// <summary>원시 파라미터 게이트(플레이어·몬스터 공용).</summary>
        public static AbilityActivationResult Evaluate(
            long nowMs, long lastCastMs, int cooldownMs,
            int manaCost, int currentMana, bool blocked)
        {
            if (blocked)
                return AbilityActivationResult.Blocked;
            if (!SkillTimelineMath.CooldownElapsed(cooldownMs, lastCastMs, nowMs))
                return AbilityActivationResult.OnCooldown;
            if (currentMana < manaCost)
                return AbilityActivationResult.NotEnoughMana;
            return AbilityActivationResult.Ok;
        }

        /// <summary><see cref="SkillTimeline"/> 편의 오버로드(플레이어 스킬).</summary>
        public static AbilityActivationResult Evaluate(
            SkillTimeline timeline, long nowMs, long lastCastMs, int currentMana, bool blocked)
            => Evaluate(nowMs, lastCastMs, timeline.CooldownMs, timeline.ManaCost, currentMana, blocked);

        /// <summary>발동 가능 여부만 필요할 때. 결과 사유가 필요하면 <see cref="Evaluate(long,long,int,int,int,bool)"/> 사용.</summary>
        public static bool CanActivate(
            long nowMs, long lastCastMs, int cooldownMs,
            int manaCost, int currentMana, bool blocked)
            => Evaluate(nowMs, lastCastMs, cooldownMs, manaCost, currentMana, blocked) == AbilityActivationResult.Ok;
    }
}
