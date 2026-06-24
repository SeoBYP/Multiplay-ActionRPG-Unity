using Game.Presentation.Progression;
using Game.System.Progression;
using NUnit.Framework;

namespace Game.Tests.PlayMode.Progression
{
    /// <summary>스탯창 ViewState 변환 — Loaded 가 9개 라인(레벨/경험치/스탯7)을 만드는지. Docker 불필요(순수 함수).</summary>
    [TestFixture]
    public class ProgressionViewStateTests
    {
        [Test]
        public void Loaded_는_레벨_경험치_스탯7_총9라인을_만든다()
        {
            var data = new ProgressionData(3, 120, 200, 60,
                new ProgressionStats(maxHealth: 140, maxMana: 50, attackPower: 18, defense: 7,
                    strength: 12, dexterity: 9, intelligence: 5));

            var state = ProgressionViewState.Loaded(data);

            Assert.IsTrue(state.HasData);
            Assert.AreEqual(9, state.Lines.Count);
            Assert.AreEqual("레벨", state.Lines[0].Label);
            Assert.AreEqual("3", state.Lines[0].Value);
            Assert.AreEqual("경험치", state.Lines[1].Label);
            Assert.AreEqual("120 / 200", state.Lines[1].Value);
            Assert.AreEqual("공격력", state.Lines[4].Label);
            Assert.AreEqual("18", state.Lines[4].Value);
        }

        [Test]
        public void 만렙이면_경험치_라인은_MAX()
        {
            var data = new ProgressionData(60, 0, 0, 60,
                new ProgressionStats(300, 100, 50, 20, 30, 25, 15));

            var state = ProgressionViewState.Loaded(data);

            Assert.AreEqual("경험치", state.Lines[1].Label);
            Assert.AreEqual("MAX", state.Lines[1].Value);
        }

        [Test]
        public void Initial_은_데이터없음에_빈_라인()
        {
            var s = ProgressionViewState.Initial;
            Assert.IsFalse(s.HasData);
            Assert.AreEqual(0, s.Lines.Count);
        }
    }
}
