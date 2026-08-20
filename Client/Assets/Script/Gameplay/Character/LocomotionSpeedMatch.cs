using UnityEngine;

namespace Game.Gameplay.Character
{
    /// <summary>
    /// 보행 클립 배속 계산 — <b>발 슬라이딩 보정</b>의 순수 로직(테스트 대상).
    ///
    /// 원리: 제자리(in-place) 보행 클립은 "이 속도로 걷는 그림"이라는 암묵적 전제를 갖는다.
    /// 실제 이동 속도가 그보다 빠르면 발이 미끄러지고, 느리면 발이 헛돈다.
    /// 클립을 <c>실제/클립</c> 배로 돌리면 두 속도가 일치한다.
    ///
    /// <b>상한을 두는 이유</b>(실측): 몬스터 클립은 편차가 커서 creepy_demon 은 0.65m/s 인데 이동은 2.2m/s(3.4배).
    /// 그대로 3.4배속으로 돌리면 다리가 우스꽝스럽다. → 배속은 <see cref="MaxMultiplier"/> 로 자르고,
    /// 남는 차이는 <b>이동 속도 쪽</b>을 낮춰서 맞춘다(몬스터 카탈로그 저작값). 절충안 C.
    /// </summary>
    public static class LocomotionSpeedMatch
    {
        /// <summary>배속 상한 — 이 이상은 다리가 과장돼 보인다. 초과분은 이동 속도를 낮춰 해소한다.</summary>
        public const float MaxMultiplier = 2.0f;

        /// <summary>배속 하한 — 너무 느리면 걷는 게 아니라 멈칫대는 것처럼 보인다.</summary>
        public const float MinMultiplier = 0.6f;

        /// <summary>
        /// 배속 = 실제/클립. <paramref name="clipSpeed"/> 가 0 이하(미저작)면 보정하지 않는다(1 반환) —
        /// 값을 모르는 몬스터를 건드리지 않기 위한 안전 기본값.
        /// 정지 상태(실제 속도 ≈ 0)에서도 1 — Idle 클립까지 느려지면 안 된다.
        /// </summary>
        public static float Multiplier(float actualSpeed, float clipSpeed)
        {
            if (clipSpeed <= 0.01f) return 1f;
            if (actualSpeed <= 0.01f) return 1f;
            return Mathf.Clamp(actualSpeed / clipSpeed, MinMultiplier, MaxMultiplier);
        }
    }
}
