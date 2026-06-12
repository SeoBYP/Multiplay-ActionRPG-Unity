namespace Script.System.GamePlayAbilitySystem
{
    /// <summary>스킬 실행 페이즈. active 동안만 hitbox를 평가한다(서버 권위 판정 구간).</summary>
    public enum ESkillPhase
    {
        Startup,
        Active,
        Recovery,
        Done,
    }

    /// <summary>hitbox 모양 (순수 기하 — 엔진 비의존).</summary>
    public enum EHitboxShape
    {
        Box,
        Sphere,
    }
}
