using System.Collections;
using System.Linq;
using Cysharp.Threading.Tasks;
using GameServer.Grpc.Inventory;
using GameServer.Grpc.Quest;
using GameServer.Grpc.Wallet;
using Grpc.Core;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.E2E
{
    /// <summary>
    /// 퀘스트 gRPC E2E (Docker 서버 대상, 4.4). 풀 루프: 수주 → Main 킬 클레임으로 서버 권위 진행 → 완료 → 보상 수령.
    /// 진행은 클라 보고가 아니라 ClaimMonsterExp(킬 클레임) 경로에서 서버가 +1 → 위조 불가. main_field_01 슬롯 1~3 = slime.
    /// </summary>
    [TestFixture]
    public class QuestE2ETests : E2ETestBase
    {
        private const string MainMap = "main_field_01";
        private const string SlimeHunt = "quest_slime_hunt"; // slime ×3 → exp50 + gold100

        [UnityTest]
        public IEnumerator 수주_킬진행_완료_보상수령_풀루프() => UniTask.ToCoroutine(async () =>
        {
            await RegisterAndLoginAsync(UniqueEmail(), "Test1234!");

            // ① 전체 카탈로그 조회 — 신규 유저는 모두 미수주.
            var initial = await QuestService.GetQuestsAsync(new GetQuestsRequest(), Timeout());
            Assert.IsTrue(initial.Result.Success, initial.Result.Message);
            Assert.GreaterOrEqual(initial.Quests.Count, 3, "시드 퀘스트 3종 이상");
            var hunt = initial.Quests.Single(q => q.QuestId == SlimeHunt);
            Assert.AreEqual(QuestProgressStatus.NotAccepted, hunt.Status);
            Assert.AreEqual(3, hunt.RequiredCount);

            // ② 수주.
            var accept = await QuestService.AcceptQuestAsync(new AcceptQuestRequest { QuestId = SlimeHunt }, Timeout());
            Assert.IsTrue(accept.Result.Success, accept.Result.Message);

            // ③ Main 슬라임 3마리 처치(슬롯 1/2/3 — 슬롯별 쿨다운이라 연속 클레임 가능) → 서버가 progress +1씩.
            for (int slot = 1; slot <= 3; slot++)
            {
                var kill = await InventoryService.ClaimMonsterExpAsync(
                    new ClaimMonsterExpRequest { MapId = MainMap, SlotId = slot }, Timeout());
                Assert.IsTrue(kill.Result.Success, $"슬롯 {slot} 킬 클레임 실패: {kill.Result.Message}");
            }

            // ④ 완료 상태 확인(progress 3/3 = Completed).
            var afterKills = await QuestService.GetQuestsAsync(new GetQuestsRequest(), Timeout());
            var huntDone = afterKills.Quests.Single(q => q.QuestId == SlimeHunt);
            Assert.AreEqual(3, huntDone.CurrentProgress, "킬 3회 후 진행 3");
            Assert.AreEqual(QuestProgressStatus.Completed, huntDone.Status);

            // ⑤ 보상 수령 → 골드 잔액 증가 확인.
            var goldBefore = (await WalletService.GetWalletAsync(new GetWalletRequest(), Timeout())).Balance;
            var claim = await QuestService.ClaimQuestRewardAsync(new ClaimQuestRewardRequest { QuestId = SlimeHunt }, Timeout());
            Assert.IsTrue(claim.Result.Success, claim.Result.Message);
            Assert.AreEqual(100, claim.Reward.Gold, "slime_hunt 보상 골드 100");

            var goldAfter = (await WalletService.GetWalletAsync(new GetWalletRequest(), Timeout())).Balance;
            Assert.AreEqual(goldBefore + 100, goldAfter, "보상 골드가 지갑에 반영");

            // ⑥ 수령 후 상태 = Claimed.
            var afterClaim = await QuestService.GetQuestsAsync(new GetQuestsRequest(), Timeout());
            Assert.AreEqual(QuestProgressStatus.Claimed,
                afterClaim.Quests.Single(q => q.QuestId == SlimeHunt).Status);
        });

        [UnityTest]
        public IEnumerator TalkToNpc_수주_대화보고_완료_보상수령() => UniTask.ToCoroutine(async () =>
        {
            const string greet = "quest_greet_elder"; // TalkToNpc npc_elder ×1 → exp30+gold50
            await RegisterAndLoginAsync(UniqueEmail(), "Test1234!");

            Assert.IsTrue((await QuestService.AcceptQuestAsync(new AcceptQuestRequest { QuestId = greet }, Timeout())).Result.Success);

            // 대상 아닌 NPC 보고 → 무진행
            await QuestService.ReportTalkAsync(new ReportTalkRequest { NpcId = "other_npc" }, Timeout());
            var mid = await QuestService.GetQuestsAsync(new GetQuestsRequest(), Timeout());
            Assert.AreEqual(QuestProgressStatus.Accepted, mid.Quests.Single(q => q.QuestId == greet).Status);

            // 대상 NPC 대화 보고 → 완료(count 1)
            var talk = await QuestService.ReportTalkAsync(new ReportTalkRequest { NpcId = "npc_elder" }, Timeout());
            Assert.IsTrue(talk.Result.Success);
            var done = await QuestService.GetQuestsAsync(new GetQuestsRequest(), Timeout());
            Assert.AreEqual(QuestProgressStatus.Completed, done.Quests.Single(q => q.QuestId == greet).Status);

            // 보상 수령
            var claim = await QuestService.ClaimQuestRewardAsync(new ClaimQuestRewardRequest { QuestId = greet }, Timeout());
            Assert.IsTrue(claim.Result.Success, claim.Result.Message);
            Assert.AreEqual(50, claim.Reward.Gold);
        });

        [UnityTest]
        public IEnumerator 미완료_보상수령은_거부된다() => UniTask.ToCoroutine(async () =>
        {
            await RegisterAndLoginAsync(UniqueEmail(), "Test1234!");
            await QuestService.AcceptQuestAsync(new AcceptQuestRequest { QuestId = SlimeHunt }, Timeout());

            // 킬 0회 → 미완료.
            var claim = await QuestService.ClaimQuestRewardAsync(new ClaimQuestRewardRequest { QuestId = SlimeHunt }, Timeout());
            Assert.IsFalse(claim.Result.Success, "미완료 퀘스트 보상수령은 실패해야 함");
        });

        [UnityTest]
        public IEnumerator 중복_수주는_거부된다() => UniTask.ToCoroutine(async () =>
        {
            await RegisterAndLoginAsync(UniqueEmail(), "Test1234!");

            Assert.IsTrue((await QuestService.AcceptQuestAsync(new AcceptQuestRequest { QuestId = SlimeHunt }, Timeout())).Result.Success);
            var second = await QuestService.AcceptQuestAsync(new AcceptQuestRequest { QuestId = SlimeHunt }, Timeout());
            Assert.IsFalse(second.Result.Success, "이미 수주한 퀘스트 재수주는 실패해야 함");
        });

        [UnityTest]
        public IEnumerator 미인증_조회는_거부된다() => UniTask.ToCoroutine(async () =>
        {
            // 로그인하지 않음 → AccessToken 없음 → AuthInterceptor 가 서비스 진입 전 Unauthenticated 로 거부(RpcException).
            RpcException caught = null;
            try
            {
                await QuestService.GetQuestsAsync(new GetQuestsRequest(), Timeout());
            }
            catch (RpcException e)
            {
                caught = e;
            }

            Assert.IsNotNull(caught, "미인증 호출인데 거부되지 않았다");
            Assert.AreEqual(StatusCode.Unauthenticated, caught.StatusCode);
        });
    }
}
