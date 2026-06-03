using System.Collections.Generic;

namespace Script.System.GamePlayAbilitySystem
{
    /// <summary>
    /// 스킬의 정적 타임라인 정의 (게임플레이 데이터 — Cue/애니/VFX는 포함하지 않음, 클라 전용).
    /// 서버는 이 데이터만으로 active window·hitbox·on-hit effect를 판정할 수 있다(애니 불필요).
    /// 1단계는 코드 카탈로그, 추후 공유 JSON 로더로 교체.
    /// </summary>
    public sealed class SkillTimeline
    {
        public string Id { get; }
        public int StartupMs { get; }
        public int ActiveMs { get; }
        public int RecoveryMs { get; }
        public int CooldownMs { get; }
        public HitboxSpec Hitbox { get; }

        /// <summary>적중 시 대상에 적용할 GameplayEffectCatalog 키들(데미지=Instant Health, 디버프=Duration 등).</summary>
        public IReadOnlyList<string> OnHitEffectIds { get; }

        public SkillTimeline(
            string id,
            int startupMs,
            int activeMs,
            int recoveryMs,
            int cooldownMs,
            HitboxSpec hitbox,
            IReadOnlyList<string> onHitEffectIds)
        {
            Id = id;
            StartupMs = startupMs;
            ActiveMs = activeMs;
            RecoveryMs = recoveryMs;
            CooldownMs = cooldownMs;
            Hitbox = hitbox;
            OnHitEffectIds = onHitEffectIds ?? new string[0];
        }

        public int ActiveStartMs => StartupMs;
        public int ActiveEndMs => StartupMs + ActiveMs;
        public int TotalMs => StartupMs + ActiveMs + RecoveryMs;
    }
}
