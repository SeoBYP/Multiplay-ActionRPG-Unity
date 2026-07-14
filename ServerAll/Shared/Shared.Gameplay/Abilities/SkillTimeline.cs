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

        /// <summary>발동 마나 코스트(서버 검증·차감 / 클라 게이트). 0 = 무료.</summary>
        public int ManaCost { get; }

        /// <summary>적중 시 대상에 적용할 GameplayEffectCatalog 키들(데미지=Instant Health, 디버프=Duration 등).</summary>
        public IReadOnlyList<string> OnHitEffectIds { get; }

        /// <summary>
        /// 콤보 체인 지점(ms) — 이 스킬이 발동한 뒤 <b>다음 공격이 나갈 수 있는 최소 시점</b>.
        /// 그 전에 들어온 입력은 클라가 선입력으로 버퍼했다가 이 시점에 발동한다(= 애니 체인 지점).
        /// <b>서버가 이 값으로 cadence 를 권위 강제</b>한다(연타 버스트 차단). 0 = 콤보 아님(게이트 없음).
        /// ※ 애니 클립의 체인 지점과 맞춰 저작한다(예: 클립 1.0s → 800).
        /// </summary>
        public int ComboChainMs { get; }

        /// <summary>
        /// 콤보 유지 창(ms) — 이 스킬 발동 후 이 시간까지 다음 입력이 없으면 콤보가 끊겨 1단계(A)부터 다시 시작.
        /// <b>불변식</b>: ComboChainMs ≤ ComboWindowMs &lt; 애니 콤보상태 유지시간(클립 × exitTime).
        /// 창이 애니 유지시간보다 길면 클라는 다음 단계로 갔는데 Animator 는 이미 Locomotion 이라 애니가 안 나온다.
        /// </summary>
        public int ComboWindowMs { get; }

        public SkillTimeline(
            string id,
            int startupMs,
            int activeMs,
            int recoveryMs,
            int cooldownMs,
            HitboxSpec hitbox,
            IReadOnlyList<string> onHitEffectIds,
            int manaCost = 0,
            int comboChainMs = 0,
            int comboWindowMs = 0)
        {
            Id = id;
            StartupMs = startupMs;
            ActiveMs = activeMs;
            RecoveryMs = recoveryMs;
            CooldownMs = cooldownMs;
            Hitbox = hitbox;
            OnHitEffectIds = onHitEffectIds ?? new string[0];
            ManaCost = manaCost;
            ComboChainMs = comboChainMs;
            ComboWindowMs = comboWindowMs;
        }

        public int ActiveStartMs => StartupMs;
        public int ActiveEndMs => StartupMs + ActiveMs;
        public int TotalMs => StartupMs + ActiveMs + RecoveryMs;
    }
}
