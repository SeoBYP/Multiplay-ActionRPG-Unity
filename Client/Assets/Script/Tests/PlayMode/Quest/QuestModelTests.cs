using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Presentation.Quest;
using Game.System.Quest;
using NUnit.Framework;
using R3;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.Quest
{
    /// <summary>클라 퀘스트 Model(MVI) 로직 — Docker 불필요(Fake 서비스). Refresh→State 병합, 수주/보상 Side Effect.</summary>
    [TestFixture]
    public class QuestModelTests
    {
        private sealed class FakeQuestService : IQuestService
        {
            private readonly QuestResult _result;
            private readonly List<QuestData> _quests;
            public string LastAccepted { get; private set; }
            public string LastClaimed { get; private set; }
            public QuestResult AcceptResult = QuestResult.Success;
            public QuestResult ClaimResult = QuestResult.Success;

            public FakeQuestService(QuestResult result, List<QuestData> quests)
            {
                _result = result;
                _quests = quests;
            }

            public UniTask<(QuestResult Result, IReadOnlyList<QuestData> Quests)> GetQuestsAsync(CancellationToken ct = default)
                => UniTask.FromResult((_result, (IReadOnlyList<QuestData>)_quests));

            public UniTask<QuestResult> AcceptAsync(string questId, CancellationToken ct = default)
            {
                LastAccepted = questId;
                return UniTask.FromResult(AcceptResult);
            }

            public UniTask<QuestResult> ClaimRewardAsync(string questId, CancellationToken ct = default)
            {
                LastClaimed = questId;
                return UniTask.FromResult(ClaimResult);
            }

            public UniTask<QuestResult> ReportTalkAsync(string npcId, CancellationToken ct = default)
                => UniTask.FromResult(QuestResult.Success);
        }

        private static QuestData Quest(string id, QuestProgressState status, int cur, int req)
            => new(id, id, "desc", QuestObjectiveKind.KillMonster, "slime", req, cur, status,
                   new QuestRewardData(50, 100, "", 0));

        [UnityTest]
        public IEnumerator Refresh_하면_퀘스트가_State에_병합된다() => UniTask.ToCoroutine(async () =>
        {
            var quests = new List<QuestData>
            {
                Quest("quest_slime_hunt", QuestProgressState.NotAccepted, 0, 3),
                Quest("quest_done", QuestProgressState.Completed, 5, 5),
            };
            var model = new QuestModel(new FakeQuestService(QuestResult.Success, quests));

            QuestState latest = null;
            using var sub = model.State.Subscribe(s => latest = s);

            model.Accept(QuestIntent.Refresh.Instance);
            await UniTask.Yield();
            await UniTask.Yield();

            Assert.AreEqual(2, latest.Quests.Count);
            Assert.IsTrue(latest.Quests[0].CanAccept);   // NotAccepted
            Assert.IsTrue(latest.Quests[1].CanClaim);    // Completed
            Assert.AreEqual("slime 처치 0/3", latest.Quests[0].ConditionText);
        });

        [UnityTest]
        public IEnumerator 수주하면_서비스에_위임되고_토스트가_뜬다() => UniTask.ToCoroutine(async () =>
        {
            var fake = new FakeQuestService(QuestResult.Success, new List<QuestData>());
            var model = new QuestModel(fake);

            string toast = null;
            using var t = model.OnToast.Subscribe(m => toast = m);

            model.Accept(new QuestIntent.Accept("quest_slime_hunt"));
            await UniTask.Yield();
            await UniTask.Yield();

            Assert.AreEqual("quest_slime_hunt", fake.LastAccepted);
            Assert.IsNotNull(toast);
        });

        [UnityTest]
        public IEnumerator 보상수령은_서비스에_위임된다() => UniTask.ToCoroutine(async () =>
        {
            var fake = new FakeQuestService(QuestResult.Success, new List<QuestData>());
            var model = new QuestModel(fake);

            model.Accept(new QuestIntent.Claim("quest_done"));
            await UniTask.Yield();
            await UniTask.Yield();

            Assert.AreEqual("quest_done", fake.LastClaimed);
        });

        [UnityTest]
        public IEnumerator 서비스_실패면_Error가_설정된다() => UniTask.ToCoroutine(async () =>
        {
            var model = new QuestModel(new FakeQuestService(QuestResult.Failed, new List<QuestData>()));

            QuestState latest = null;
            using var sub = model.State.Subscribe(s => latest = s);

            model.Accept(QuestIntent.Refresh.Instance);
            await UniTask.Yield();
            await UniTask.Yield();

            Assert.IsNotNull(latest.Error);
        });
    }
}
