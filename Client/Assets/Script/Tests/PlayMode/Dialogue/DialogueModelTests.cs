using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.System.Quest;
using Game.Presentation.Dialogue;
using NUnit.Framework;
using R3;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.Dialogue
{
    /// <summary>대화 엔진(DialogueModel) 로직 — 노드 순회(GoTo)·종료(EndDialogue)·콘텐츠 없음. Docker/서버 무관.</summary>
    [TestFixture]
    public class DialogueModelTests
    {
        // start →[다음]GoTo n2 / n2 →[끝]EndDialogue
        private static DialogueDefinition BuildDef()
        {
            var def = ScriptableObject.CreateInstance<DialogueDefinition>();
            var start = new DialogueNode
            {
                id = "start", speaker = "촌장", bodyText = "어서 오게.",
                shot = Game.System.Dialogue.DialogueShot.OverShoulder, // 카메라 테스트용 구도
                choices = new List<DialogueChoice>
                {
                    new() { label = "다음", action = DialogueActionKind.GoTo, targetNodeId = "n2" },
                },
            };
            var n2 = new DialogueNode
            {
                id = "n2", speaker = "촌장", bodyText = "잘 가게.",
                shot = Game.System.Dialogue.DialogueShot.Closeup,
                choices = new List<DialogueChoice>
                {
                    new() { label = "끝", action = DialogueActionKind.EndDialogue },
                },
            };
            typeof(DialogueDefinition).GetField("nodes", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(def, new List<DialogueNode> { start, n2 });
            def.startNodeId = "start";
            return def;
        }

        private static DialogueCatalog BuildCatalog(string npcId, DialogueDefinition def)
        {
            var catalog = ScriptableObject.CreateInstance<DialogueCatalog>();
            var entry = new DialogueCatalog.Entry { npcId = npcId, dialogue = def };
            typeof(DialogueCatalog).GetField("entries", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(catalog, new[] { entry });
            return catalog;
        }

        [UnityTest]
        public IEnumerator Start_하면_시작노드와_선택지가_표시된다() => UniTask.ToCoroutine(async () =>
        {
            var model = new DialogueModel(BuildCatalog("villager", BuildDef()));
            DialogueState latest = null;
            using var sub = model.State.Subscribe(s => latest = s);

            model.Start("villager");
            await UniTask.Yield();

            Assert.IsTrue(latest.IsOpen);
            Assert.AreEqual("어서 오게.", latest.BodyText);
            Assert.AreEqual(1, latest.Choices.Count);
            Assert.AreEqual("다음", latest.Choices[0].Label);

            model.Dispose();
        });

        [UnityTest]
        public IEnumerator GoTo_선택지는_대상_노드로_이동한다() => UniTask.ToCoroutine(async () =>
        {
            var model = new DialogueModel(BuildCatalog("villager", BuildDef()));
            DialogueState latest = null;
            using var sub = model.State.Subscribe(s => latest = s);

            model.Start("villager");
            model.Accept(new DialogueIntent.SelectChoice(0)); // GoTo n2
            await UniTask.Yield();

            Assert.IsTrue(latest.IsOpen);
            Assert.AreEqual("잘 가게.", latest.BodyText);

            model.Dispose();
        });

        [UnityTest]
        public IEnumerator EndDialogue_선택지는_대화를_닫는다() => UniTask.ToCoroutine(async () =>
        {
            var model = new DialogueModel(BuildCatalog("villager", BuildDef()));
            DialogueState latest = null;
            using var sub = model.State.Subscribe(s => latest = s);

            model.Start("villager");
            model.Accept(new DialogueIntent.SelectChoice(0)); // → n2
            model.Accept(new DialogueIntent.SelectChoice(0)); // EndDialogue
            await UniTask.Yield();

            Assert.IsFalse(latest.IsOpen);

            model.Dispose();
        });

        [UnityTest]
        public IEnumerator 콘텐츠가_없는_npc는_열리지_않는다() => UniTask.ToCoroutine(async () =>
        {
            var model = new DialogueModel(BuildCatalog("villager", BuildDef()));
            DialogueState latest = null;
            using var sub = model.State.Subscribe(s => latest = s);

            model.Start("unknown_npc");
            await UniTask.Yield();

            Assert.IsFalse(latest.IsOpen);

            model.Dispose();
        });

        // ── Phase B: 퀘스트 연동 ──

        private sealed class FakeQuestService : IQuestService
        {
            public QuestProgressState State = QuestProgressState.NotAccepted;
            public string Accepted, Claimed;

            /// <summary>대화 목표 퀘스트 시드(F5 클라 게이트 검증용). null 이면 대화 목표 없음.</summary>
            public string TalkNpcId;
            public QuestProgressState TalkState = QuestProgressState.Accepted;

            /// <summary>서버 왕복 횟수 — "잡담 NPC 는 통신 0회" 를 관측한다.</summary>
            public int GetQuestsCalls;

            public UniTask<(QuestResult, IReadOnlyList<QuestData>)> GetQuestsAsync(CancellationToken ct = default)
            {
                GetQuestsCalls++;
                var list = new List<QuestData>
                {
                    new("q1", "q1", "", QuestObjectiveKind.KillMonster, "creepy_demon", 1, 0, State, default),
                };
                if (TalkNpcId != null)
                    list.Add(new("q_talk", "q_talk", "", QuestObjectiveKind.TalkToNpc, TalkNpcId, 1, 0, TalkState, default));
                return UniTask.FromResult((QuestResult.Success, (IReadOnlyList<QuestData>)list));
            }

            public UniTask<QuestResult> AcceptAsync(string questId, CancellationToken ct = default)
            { Accepted = questId; return UniTask.FromResult(QuestResult.Success); }

            public UniTask<QuestResult> ClaimRewardAsync(string questId, CancellationToken ct = default)
            { Claimed = questId; return UniTask.FromResult(QuestResult.Success); }

            public string Talked;
            public UniTask<QuestResult> ReportTalkAsync(string npcId, CancellationToken ct = default)
            { Talked = npcId; return UniTask.FromResult(QuestResult.Success); }
        }

        // start: [수락](AcceptQuest q1, showIf=NotAccepted) / [보상](ClaimQuest q1, showIf=Completed)
        private static DialogueDefinition BuildQuestDef()
        {
            var def = ScriptableObject.CreateInstance<DialogueDefinition>();
            var start = new DialogueNode
            {
                id = "start", speaker = "촌장", bodyText = "일을 맡겠나?",
                choices = new List<DialogueChoice>
                {
                    new() { label = "수락", action = DialogueActionKind.AcceptQuest, questId = "q1",
                            showIf = DialogueShowCondition.QuestNotAccepted, conditionQuestId = "q1" },
                    new() { label = "보상", action = DialogueActionKind.ClaimQuest, questId = "q1",
                            showIf = DialogueShowCondition.QuestCompleted, conditionQuestId = "q1" },
                },
            };
            typeof(DialogueDefinition).GetField("nodes", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(def, new List<DialogueNode> { start });
            def.startNodeId = "start";
            return def;
        }

        [UnityTest]
        public IEnumerator 미수주_상태면_수락_선택지만_보인다() => UniTask.ToCoroutine(async () =>
        {
            var fake = new FakeQuestService { State = Game.System.Quest.QuestProgressState.NotAccepted };
            var model = new DialogueModel(BuildCatalog("npc", BuildQuestDef()), quest: fake);
            DialogueState latest = null;
            using var sub = model.State.Subscribe(s => latest = s);

            model.Start("npc");
            await UniTask.Yield();
            await UniTask.Yield();

            Assert.AreEqual(1, latest.Choices.Count);
            Assert.AreEqual("수락", latest.Choices[0].Label);

            model.Dispose();
        });

        [UnityTest]
        public IEnumerator 완료_상태면_보상_선택지만_보인다() => UniTask.ToCoroutine(async () =>
        {
            var fake = new FakeQuestService { State = Game.System.Quest.QuestProgressState.Completed };
            var model = new DialogueModel(BuildCatalog("npc", BuildQuestDef()), quest: fake);
            DialogueState latest = null;
            using var sub = model.State.Subscribe(s => latest = s);

            model.Start("npc");
            await UniTask.Yield();
            await UniTask.Yield();

            Assert.AreEqual(1, latest.Choices.Count);
            Assert.AreEqual("보상", latest.Choices[0].Label);

            model.Dispose();
        });

        [UnityTest]
        public IEnumerator 수락_선택지는_QuestService에_수주를_위임한다() => UniTask.ToCoroutine(async () =>
        {
            var fake = new FakeQuestService { State = Game.System.Quest.QuestProgressState.NotAccepted };
            var model = new DialogueModel(BuildCatalog("npc", BuildQuestDef()), quest: fake);
            using var sub = model.State.Subscribe(_ => { });

            model.Start("npc");
            await UniTask.Yield();
            await UniTask.Yield();
            model.Accept(new DialogueIntent.SelectChoice(0)); // 수락
            await UniTask.Yield();
            await UniTask.Yield();

            Assert.AreEqual("q1", fake.Accepted);

            model.Dispose();
        });

        // ── Phase C: TalkToNpc 보고 ──

        // ── F5: 필요할 때만 서버와 통신한다 ──
        //
        // 예전엔 대화창을 열 때마다 무조건 ReportTalk 을 불렀다. 서버는 그 요청이 정상인지 알 수 없었고
        // 잡담 NPC 도 매번 왕복을 만들었다. 이제 판단은 클라가 한다:
        //   hasQuest(저작 플래그) → 조기 차단 / 수주 상태 → 실제 보고 여부

        [UnityTest]
        public IEnumerator 수주중인_대화퀘스트가_있으면_보고한다() => UniTask.ToCoroutine(async () =>
        {
            var fake = new FakeQuestService { TalkNpcId = "npc_elder", TalkState = QuestProgressState.Accepted };
            var model = new DialogueModel(BuildCatalog("npc_elder", BuildDef()), quest: fake);
            using var sub = model.State.Subscribe(_ => { });

            model.Start("npc_elder");
            for (int i = 0; i < 4; i++) await UniTask.Yield();

            Assert.AreEqual("npc_elder", fake.Talked);

            model.Dispose();
        });

        [UnityTest]
        public IEnumerator 수주하지_않은_NPC_에는_보고하지_않는다() => UniTask.ToCoroutine(async () =>
        {
            var fake = new FakeQuestService { TalkNpcId = "npc_elder", TalkState = QuestProgressState.NotAccepted };
            var model = new DialogueModel(BuildCatalog("npc_elder", BuildDef()), quest: fake);
            using var sub = model.State.Subscribe(_ => { });

            model.Start("npc_elder");
            for (int i = 0; i < 4; i++) await UniTask.Yield();

            Assert.IsNull(fake.Talked, "수주하지 않았는데 보고했다");

            model.Dispose();
        });

        [UnityTest]
        public IEnumerator 완료된_대화퀘스트에는_다시_보고하지_않는다() => UniTask.ToCoroutine(async () =>
        {
            var fake = new FakeQuestService { TalkNpcId = "npc_elder", TalkState = QuestProgressState.Completed };
            var model = new DialogueModel(BuildCatalog("npc_elder", BuildDef()), quest: fake);
            using var sub = model.State.Subscribe(_ => { });

            model.Start("npc_elder");
            for (int i = 0; i < 4; i++) await UniTask.Yield();

            Assert.IsNull(fake.Talked);

            model.Dispose();
        });

        [UnityTest]
        public IEnumerator 잡담_NPC_는_퀘스트_통신을_아예_하지_않는다() => UniTask.ToCoroutine(async () =>
        {
            var fake = new FakeQuestService { TalkNpcId = "npc_elder", TalkState = QuestProgressState.Accepted };
            var model = new DialogueModel(BuildCatalog("npc_elder", BuildDef()), quest: fake);
            DialogueState latest = null;
            using var sub = model.State.Subscribe(s => latest = s);

            model.Start("npc_elder", hasQuest: false);   // 저작 플래그로 조기 차단
            for (int i = 0; i < 4; i++) await UniTask.Yield();

            Assert.AreEqual(0, fake.GetQuestsCalls, "잡담 NPC 인데 퀘스트를 조회했다");
            Assert.IsNull(fake.Talked);
            Assert.IsTrue(latest.IsOpen, "통신을 건너뛰어도 대화는 정상적으로 열려야 한다");

            model.Dispose();
        });

        // ── 카메라(A3)/입력점유 배선 ──

        private sealed class FakeDialogueCamera : Game.System.Dialogue.IDialogueCamera
        {
            public readonly List<Game.System.Dialogue.DialogueShot> Shots = new();
            public int EnterCount, ExitCount;
            public void Enter(UnityEngine.Transform npc, UnityEngine.Transform player) => EnterCount++;
            public void SetShot(Game.System.Dialogue.DialogueShot shot) => Shots.Add(shot);
            public void Exit() => ExitCount++;
        }

        private sealed class FakeInputContext : Game.System.Input.IInputContext
        {
            public int Enter, Exit;
            public void EnterUi() => Enter++;
            public void ExitUi() => Exit++;
            public bool IsUiActive => Enter > Exit;
        }

        [UnityTest]
        public IEnumerator 노드_진입마다_카메라_구도를_설정한다() => UniTask.ToCoroutine(async () =>
        {
            var cam = new FakeDialogueCamera();
            var model = new DialogueModel(BuildCatalog("npc", BuildDef()), camera: cam);
            using var sub = model.State.Subscribe(_ => { });

            model.Start("npc"); // start.shot=OverShoulder
            await UniTask.Yield();
            await UniTask.Yield();
            model.Accept(new DialogueIntent.SelectChoice(0)); // GoTo n2(shot=Closeup)
            await UniTask.Yield();

            Assert.AreEqual(Game.System.Dialogue.DialogueShot.OverShoulder, cam.Shots[0]);
            Assert.AreEqual(Game.System.Dialogue.DialogueShot.Closeup, cam.Shots[1]);

            model.Dispose();
        });

        [UnityTest]
        public IEnumerator 종료시_카메라Exit하고_입력점유_해제한다() => UniTask.ToCoroutine(async () =>
        {
            var cam = new FakeDialogueCamera();
            var input = new FakeInputContext();
            var model = new DialogueModel(BuildCatalog("npc", BuildDef()), inputContext: input, camera: cam);
            using var sub = model.State.Subscribe(_ => { });

            model.Start("npc");
            await UniTask.Yield();
            await UniTask.Yield();
            Assert.IsTrue(input.IsUiActive, "대화 시작 시 입력 점유");

            model.Accept(new DialogueIntent.SelectChoice(0)); // → n2
            model.Accept(new DialogueIntent.SelectChoice(0)); // EndDialogue → 종료
            await UniTask.Yield();

            Assert.AreEqual(1, cam.ExitCount);
            Assert.IsFalse(input.IsUiActive, "종료 시 입력 점유 해제");

            model.Dispose();
        });

        // ── 네비게이션/엣지 ──

        [UnityTest]
        public IEnumerator 끊긴_GoTo_선택지는_대화를_닫는다() => UniTask.ToCoroutine(async () =>
        {
            var def = ScriptableObject.CreateInstance<DialogueDefinition>();
            var start = new DialogueNode
            {
                id = "start", bodyText = "끊긴 엣지",
                choices = new List<DialogueChoice>
                {
                    new() { label = "없는 곳", action = DialogueActionKind.GoTo, targetNodeId = "ghost" },
                },
            };
            typeof(DialogueDefinition).GetField("nodes", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(def, new List<DialogueNode> { start });
            def.startNodeId = "start";

            var model = new DialogueModel(BuildCatalog("npc", def));
            DialogueState latest = null;
            using var sub = model.State.Subscribe(s => latest = s);

            model.Start("npc");
            await UniTask.Yield();
            await UniTask.Yield();
            model.Accept(new DialogueIntent.SelectChoice(0)); // GoTo ghost(없음) → 닫힘
            await UniTask.Yield();

            Assert.IsFalse(latest.IsOpen);

            model.Dispose();
        });

        [UnityTest]
        public IEnumerator 시작노드_미지정이면_첫_노드를_연다() => UniTask.ToCoroutine(async () =>
        {
            var def = ScriptableObject.CreateInstance<DialogueDefinition>();
            var first = new DialogueNode { id = "a", bodyText = "첫 노드", choices = new List<DialogueChoice>() };
            typeof(DialogueDefinition).GetField("nodes", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(def, new List<DialogueNode> { first });
            def.startNodeId = null; // 미지정 → 첫 노드 폴백

            var model = new DialogueModel(BuildCatalog("npc", def));
            DialogueState latest = null;
            using var sub = model.State.Subscribe(s => latest = s);

            model.Start("npc");
            await UniTask.Yield();
            await UniTask.Yield();

            Assert.IsTrue(latest.IsOpen);
            Assert.AreEqual("첫 노드", latest.BodyText);

            model.Dispose();
        });
    }
}
