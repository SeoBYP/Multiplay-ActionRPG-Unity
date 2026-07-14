using Game.Gameplay.Character;
using NUnit.Framework;

namespace Game.Tests.EditMode.Gameplay
{
    /// <summary>
    /// #7 근접 콤보 진행기 — 선입력(버퍼링) + <b>타이밍은 스킬 데이터가 진실원</b>.
    /// 체인 지점/창은 skillId 별로 다를 수 있고(SkillTimeline.ComboChainMs/ComboWindowMs), 서버 cadence 도 같은 값을 쓴다.
    /// skillId 규약: 2=combo_a, 3=combo_b, 4=combo_c.
    /// </summary>
    public class ComboDriverTests
    {
        // 저작값 모사(skills.json): A/B = chain 0.8 / window 0.9,  C = chain 0.9 / window 1.0
        private static (float chainSec, float windowSec) Timing(int skillId) => skillId switch
        {
            4 => (0.9f, 1.0f), // combo_c — 마무리라 조금 길다(단계별로 다른 값을 쓰는지 검증)
            _ => (0.8f, 0.9f), // combo_a / combo_b
        };

        private static ComboDriver New() => new ComboDriver(new[] { 2, 3, 4 }, Timing);

        /// <summary>입력 접수 + 그 프레임 발동 시도(그 자리에서 나갔는지 반환).</summary>
        private static bool Press(ComboDriver c, float now, out int skillId, out int step)
        {
            c.OnAttackPressed(now);
            return c.TryFire(now, out skillId, out step);
        }

        [Test]
        public void 첫타A는_입력_즉시_발동한다()
        {
            var combo = New();
            Assert.IsTrue(Press(combo, 0f, out int s, out int st));
            Assert.AreEqual((2, 0), (s, st), "첫 입력은 A(2,0) 즉시");
        }

        [Test]
        public void 체인_지점_전_선입력은_버려지지_않고_그_시점에_발동한다()
        {
            // 핵심: 스윙 도중(0.3s) 미리 눌러도 입력이 사라지지 않고, A 의 체인 지점(0.8s)에 자동으로 이어진다.
            var combo = New();
            Assert.IsTrue(Press(combo, 0f, out _, out _)); // A

            combo.OnAttackPressed(0.3f); // 선입력(스윙 도중)
            Assert.IsFalse(combo.TryFire(0.3f, out _, out _), "체인 지점 전에는 아직 안 나간다");
            Assert.IsFalse(combo.TryFire(0.79f, out _, out _), "0.79s 도 아직");

            Assert.IsTrue(combo.TryFire(0.8f, out int s, out int st), "A 의 체인 지점(0.8s)에 버퍼된 입력이 발동");
            Assert.AreEqual((3, 1), (s, st), "버퍼된 입력은 B(3,1)");
        }

        [Test]
        public void 체인_지점_이후_입력은_즉시_발동한다()
        {
            var combo = New();
            Assert.IsTrue(Press(combo, 0f, out _, out _)); // A

            // 0.85s = A 의 chain(0.8) 지남 + window(0.9) 안 → 즉시 B.
            Assert.IsTrue(Press(combo, 0.85f, out int s, out int st));
            Assert.AreEqual((3, 1), (s, st));
        }

        [Test]
        public void 콤보는_A_B_C_순으로_진행하고_다시_A로_순환한다()
        {
            var combo = New();
            Assert.IsTrue(Press(combo, 0f, out int s, out int st));
            Assert.AreEqual((2, 0), (s, st));

            Assert.IsTrue(Press(combo, 0.8f, out s, out st));   // A 의 chain
            Assert.AreEqual((3, 1), (s, st));

            Assert.IsTrue(Press(combo, 1.6f, out s, out st));   // B 의 chain(0.8)
            Assert.AreEqual((4, 2), (s, st));

            Assert.IsTrue(Press(combo, 2.5f, out s, out st));   // C 의 chain(0.9)
            Assert.AreEqual((2, 0), (s, st), "C 다음은 다시 A");
        }

        [Test]
        public void 단계별로_다른_체인_지점을_쓴다()
        {
            // C 는 chain 0.9 — A/B(0.8) 보다 길다. 데이터가 단계별로 다르게 먹히는지 고정.
            var combo = New();
            Press(combo, 0f, out _, out _);    // A  (chain 0.8)
            Press(combo, 0.8f, out _, out _);  // B  (chain 0.8)
            Press(combo, 1.6f, out _, out _);  // C  (chain 0.9)

            combo.OnAttackPressed(1.7f); // C 스윙 도중 선입력
            Assert.IsFalse(combo.TryFire(2.45f, out _, out _), "C 의 체인 지점(1.6+0.9=2.5s) 전에는 안 나간다");
            Assert.IsTrue(combo.TryFire(2.5f, out int s, out int st), "C 의 체인 지점에 발동");
            Assert.AreEqual((2, 0), (s, st), "C 다음은 A");
        }

        [Test]
        public void 창이_지나면_A로_리셋된다()
        {
            var combo = New();
            Assert.IsTrue(Press(combo, 0f, out _, out _)); // A (window 0.9)

            // 마지막 스윙(0) 후 창(0.9s) 초과 → 콤보 끊김 → A 부터.
            Assert.IsTrue(Press(combo, 2.0f, out int s, out int st));
            Assert.AreEqual((2, 0), (s, st), "창 만료 후에는 A 부터");
        }

        [Test]
        public void Reset하면_선입력도_지워지고_다음_입력은_A부터()
        {
            var combo = New();
            Press(combo, 0f, out _, out _);  // A
            combo.OnAttackPressed(0.3f);     // 선입력 버퍼
            combo.Reset();

            Assert.IsFalse(combo.TryFire(0.9f, out _, out _), "Reset 하면 버퍼된 선입력도 사라진다");

            Assert.IsTrue(Press(combo, 1.0f, out int s, out int st));
            Assert.AreEqual((2, 0), (s, st), "Reset 후 첫 입력은 A(게이트 없음)");
        }
    }
}
