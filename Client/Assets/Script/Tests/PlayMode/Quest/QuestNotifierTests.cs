using System.Collections.Generic;
using Game.Presentation.Quest;
using Game.System.Quest;
using NUnit.Framework;
using R3;

namespace Game.Tests.PlayMode.Quest
{
    /// <summary>QuestNotifier 단위 — 완료 전이 1회 알림 + 수락/보상 메시지. Docker 불필요(순수 C#).</summary>
    [TestFixture]
    public class QuestNotifierTests
    {
        private static QuestData Q(string id, QuestProgressState status,
            string name = "quest", int exp = 0, int gold = 0)
            => new(id, name, "desc", QuestObjectiveKind.KillMonster, "creepy_demon", 3, 0, status,
                   new QuestRewardData(exp, gold, "", 0));

        [Test]
        public void 완료로_전이할때만_완료알림이_뜬다()
        {
            var notifier = new QuestNotifier();
            var notices = new List<QuestNotice>();
            using var sub = notifier.OnNotice.Subscribe(notices.Add);

            notifier.Sync(new[] { Q("q1", QuestProgressState.Accepted) });
            Assert.AreEqual(0, notices.Count, "수락 상태는 완료알림 없음");

            notifier.Sync(new[] { Q("q1", QuestProgressState.Completed) });
            Assert.AreEqual(1, notices.Count);
            Assert.AreEqual(QuestNoticeKind.Completed, notices[0].Kind);

            notifier.Sync(new[] { Q("q1", QuestProgressState.Completed) }); // 재조회 — 재알림 X
            Assert.AreEqual(1, notices.Count, "이미 완료 관측 후 재조회는 재알림 금지");
        }

        [Test]
        public void 보상수령_알림은_보상요약을_포함한다()
        {
            var notifier = new QuestNotifier();
            var notices = new List<QuestNotice>();
            using var sub = notifier.OnNotice.Subscribe(notices.Add);

            notifier.Sync(new[] { Q("q1", QuestProgressState.Completed, exp: 50, gold: 100) });
            notices.Clear();

            notifier.NotifyClaimed("q1");
            Assert.AreEqual(1, notices.Count);
            Assert.AreEqual(QuestNoticeKind.Claimed, notices[0].Kind);
            StringAssert.Contains("경험치 50", notices[0].Message);
            StringAssert.Contains("골드 100", notices[0].Message);
        }

        [Test]
        public void 수락_알림은_퀘스트_이름을_쓴다()
        {
            var notifier = new QuestNotifier();
            var notices = new List<QuestNotice>();
            using var sub = notifier.OnNotice.Subscribe(notices.Add);

            notifier.Sync(new[] { Q("q1", QuestProgressState.Accepted, name: "슬라임 사냥") });
            notices.Clear();

            notifier.NotifyAccepted("q1");
            Assert.AreEqual(QuestNoticeKind.Accepted, notices[0].Kind);
            StringAssert.Contains("슬라임 사냥", notices[0].Message);
        }
    }
}
