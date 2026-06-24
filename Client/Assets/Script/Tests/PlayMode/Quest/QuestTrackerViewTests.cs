using System.Collections.Generic;
using Game.GUI.Hud;
using Game.Presentation.Quest;
using Game.System.Quest;
using NUnit.Framework;

namespace Game.Tests.PlayMode.Quest
{
    /// <summary>QuestTrackerView 진행중 필터 — 미수주/수령완료 제외, Accepted/Completed 만. Docker 불필요(순수 함수).</summary>
    [TestFixture]
    public class QuestTrackerViewTests
    {
        private static QuestEntryModel Q(string id, QuestProgressState status)
            => new(id, id, "desc", 3, 1, status, "", "", "cond", false, new List<string>());

        [Test]
        public void 진행중만_추린다_미수주와_수령완료는_제외된다()
        {
            var quests = new List<QuestEntryModel>
            {
                Q("a", QuestProgressState.NotAccepted), // 제외
                Q("b", QuestProgressState.Accepted),    // 포함
                Q("c", QuestProgressState.Completed),   // 포함(보상받기 대기)
                Q("d", QuestProgressState.Claimed),     // 제외
            };

            var result = QuestTrackerView.InProgress(quests);

            Assert.AreEqual(2, result.Count);
            CollectionAssert.AreEquivalent(new[] { "b", "c" }, result.ConvertAll(q => q.QuestId));
        }

        [Test]
        public void null이면_빈_목록을_반환한다()
        {
            Assert.AreEqual(0, QuestTrackerView.InProgress(null).Count);
        }
    }
}
