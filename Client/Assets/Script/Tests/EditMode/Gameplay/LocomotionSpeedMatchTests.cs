using Game.Gameplay.Character;
using NUnit.Framework;

namespace Game.Tests.EditMode.Gameplay
{
    /// <summary>
    /// 발 슬라이딩 보정 배속(절충안 C). 상한을 두는 게 핵심 — 몬스터 클립은 편차가 커서
    /// 무제한 배속이면 다리가 우스꽝스러워진다(실측 최대 3.4배 필요). 초과분은 이동 속도로 해소한다.
    /// </summary>
    public class LocomotionSpeedMatchTests
    {
        [Test]
        public void 클립속도_미저작이면_보정하지_않는다()
        {
            Assert.AreEqual(1f, LocomotionSpeedMatch.Multiplier(2.2f, 0f), 0.001f,
                "값을 모르는 몬스터는 건드리지 않는다(안전 기본값).");
        }

        [Test]
        public void 정지_상태에서는_배속이_1이다()
        {
            Assert.AreEqual(1f, LocomotionSpeedMatch.Multiplier(0f, 1.5f), 0.001f,
                "멈춰 있으면 Idle 클립까지 느려지면 안 된다.");
        }

        [Test]
        public void 실제속도와_클립속도의_비율이_배속이_된다()
        {
            Assert.AreEqual(1.5f, LocomotionSpeedMatch.Multiplier(3.0f, 2.0f), 0.001f);
            Assert.AreEqual(0.8f, LocomotionSpeedMatch.Multiplier(1.6f, 2.0f), 0.001f,
                "클립이 더 빠르면(발이 헛돎) 1 미만으로 낮춘다 — arachnya 사례.");
        }

        [Test]
        public void 배속은_상하한으로_잘린다()
        {
            // creepy_demon: 클립 0.65 / 이동 2.2 → 3.39 배가 필요하지만 상한에서 잘린다.
            Assert.AreEqual(LocomotionSpeedMatch.MaxMultiplier,
                LocomotionSpeedMatch.Multiplier(2.2f, 0.65f), 0.001f,
                "상한 초과분은 배속이 아니라 이동 속도(카탈로그 저작값)로 해소한다.");

            Assert.AreEqual(LocomotionSpeedMatch.MinMultiplier,
                LocomotionSpeedMatch.Multiplier(0.2f, 3.19f), 0.001f,
                "너무 느리면 걷는 게 아니라 멈칫대는 것처럼 보인다.");
        }

        [Test]
        public void 속도를_낮춘_몬스터는_상한_배속에서_정확히_일치한다()
        {
            // 절충 결과 고정: creepy_demon 이동 1.30 = 클립 0.65 × 상한 2.0 → 배속 정확히 상한.
            Assert.AreEqual(2.0f, LocomotionSpeedMatch.Multiplier(1.30f, 0.65f), 0.001f);
            // undead_axemaster 1.54 = 0.77 × 2.0
            Assert.AreEqual(2.0f, LocomotionSpeedMatch.Multiplier(1.54f, 0.77f), 0.001f);
        }
    }
}
