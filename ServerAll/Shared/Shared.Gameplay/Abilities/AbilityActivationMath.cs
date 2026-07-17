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
    /// 발동 규칙 이중정의를 원천 차단한다 — actor-combat-architecture.md §3(발동 규칙 단일화).
    ///
    /// 원시 파라미터를 받는 이유: 플레이어(<see cref="SkillTimeline"/> 의 CooldownMs/ManaCost)와
    /// 몬스터(MonsterDef 의 AttackCooldownMs)가 <b>같은 게이트</b>를 쓰되 각자 자기 데이터에서 값을 먹인다.
    ///
    /// 우선순위: Blocked → OnCooldown → NotEnoughMana → Ok.
    /// (죽었으면 쿨다운·마나와 무관하게 못 쓴다 → Blocked 최우선.)
    /// </summary>
    public static class AbilityActivationMath
    {
        /// <summary>원시 파라미터 게이트(플레이어·몬스터 공용). 쿨다운 경과 판정은 <see cref="SkillTimelineMath.CooldownElapsed"/> 재사용.</summary>
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

        /// <summary><see cref="SkillTimeline"/> 편의 오버로드(플레이어 스킬). 쿨다운·마나 코스트를 타임라인에서 뽑는다.</summary>
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
