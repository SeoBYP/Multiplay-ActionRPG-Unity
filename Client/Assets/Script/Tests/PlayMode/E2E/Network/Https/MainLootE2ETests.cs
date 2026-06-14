using System.Collections;
using System.Linq;
using Cysharp.Threading.Tasks;
using GameServer.Grpc.Inventory;
using GameServer.Grpc.Progression;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.E2E
{
    /// <summary>
    /// Main(싱글) 획득 경로 E2E (Docker 서버 대상) — B-lite 서버 검증. 던전 풀 E2E(SocketE2ETests)의 Main 대응판.
    ///
    /// 검증: 클라는 (mapId, slotId)만 보고(ClaimKill) → **서버가 슬롯 검증 + 권위 roll + 지급**.
    ///   - 유효 슬롯 처치 → slime 보장 드랍(potion_hp_small)이 인벤토리에 반영.
    ///   - 무한 스폰·재청구(쿨다운 내) → 보상 없음 = 무한 파밍 차단(핵 직접 증명).
    ///   - 위조 슬롯 → 거부.
    /// 정본 = docs/wiki/main-spawn-claim.md / authority-model §4b.
    /// (MonoBehaviour 글루 — LocalMonster/LocalGroundItem 입력·콜라이더 — 는 플레이 검증 영역.)
    /// </summary>
    [TestFixture]
    public class MainLootE2ETests : E2ETestBase
    {
        private const string MainMap = "main_field_01";

        [UnityTest]
        public IEnumerator Main_유효슬롯_클레임하면_서버roll로_인벤토리에_반영된다() => UniTask.ToCoroutine(async () =>
        {
            await RegisterAndLoginAsync(UniqueEmail(), "Test1234!");

            // 클라는 슬롯만 지목 — 보상 내용은 서버가 결정(권위 roll). slime 슬롯 1은 potion 보장 드랍.
            var claim = await InventoryService.ClaimKillAsync(
                new ClaimKillRequest { MapId = MainMap, SlotId = 1 }, Timeout());

            Assert.IsTrue(claim.Result.Success, claim.Result.Message);
            Assert.IsTrue(claim.Granted.Any(g => g.ItemId == "potion_hp_small"), "서버 roll 보장 드랍 potion 누락");

            // 진실원 = 서버 DB.
            var inv = await InventoryService.GetInventoryAsync(new GetInventoryRequest(), Timeout());
            var item = inv.Items.FirstOrDefault(i => i.ItemId == "potion_hp_small");
            Assert.IsNotNull(item, "potion_hp_small 가 인벤토리에 반영되지 않았다");
            Assert.GreaterOrEqual(item.Quantity, 1);
        });

        [UnityTest]
        public IEnumerator Main_쿨다운_내_재청구는_지급되지_않는다_무한파밍_차단() => UniTask.ToCoroutine(async () =>
        {
            await RegisterAndLoginAsync(UniqueEmail(), "Test1234!");

            // 1회차: 정상 지급.
            var first = await InventoryService.ClaimKillAsync(new ClaimKillRequest { MapId = MainMap, SlotId = 2 }, Timeout());
            Assert.IsTrue(first.Result.Success, first.Result.Message);
            Assert.IsNotEmpty(first.Granted);
            var after1 = (await InventoryService.GetInventoryAsync(new GetInventoryRequest(), Timeout()))
                .Items.First(i => i.ItemId == "potion_hp_small").Quantity;

            // 2회차(쿨다운 내, 무한 스폰 후 재청구 시도): 성공이지만 보상 0 → 인벤토리 불변 = 파밍 차단.
            var second = await InventoryService.ClaimKillAsync(new ClaimKillRequest { MapId = MainMap, SlotId = 2 }, Timeout());
            Assert.IsTrue(second.Result.Success);
            Assert.IsEmpty(second.Granted);
            var after2 = (await InventoryService.GetInventoryAsync(new GetInventoryRequest(), Timeout()))
                .Items.First(i => i.ItemId == "potion_hp_small").Quantity;
            Assert.AreEqual(after1, after2, "쿨다운 내 재청구로 수량이 늘면 무한 파밍 가능");
        });

        [UnityTest]
        public IEnumerator Main_킬_즉시_ClaimMonsterExp로_경험치가_진행에_반영된다() => UniTask.ToCoroutine(async () =>
        {
            await RegisterAndLoginAsync(UniqueEmail(), "Test1234!");

            // 킬 즉시 exp 청구(줍기와 독립). slime(MonsterCatalog expReward=20) → 20 < Lv1 임계(100) → Lv1/Exp20.
            var exp = await InventoryService.ClaimMonsterExpAsync(
                new ClaimMonsterExpRequest { MapId = MainMap, SlotId = 3 }, Timeout());

            Assert.IsTrue(exp.Result.Success, exp.Result.Message);
            Assert.AreEqual(20, exp.ExpGained, "ClaimMonsterExp 응답에 획득 exp");

            // 진실원 = 서버 DB (GetProgression).
            var prog = await ProgressionService.GetProgressionAsync(new GetProgressionRequest(), Timeout());
            Assert.IsTrue(prog.Result.Success, prog.Result.Message);
            Assert.AreEqual(1, prog.Level);
            Assert.AreEqual(20, prog.Exp, "Main 킬 exp 가 진행에 적립되지 않았다");
        });

        [UnityTest]
        public IEnumerator Main_위조슬롯_클레임은_거부된다() => UniTask.ToCoroutine(async () =>
        {
            await RegisterAndLoginAsync(UniqueEmail(), "Test1234!");

            var claim = await InventoryService.ClaimKillAsync(
                new ClaimKillRequest { MapId = MainMap, SlotId = 999 }, Timeout());

            Assert.IsFalse(claim.Result.Success, "맵에 없는 슬롯은 거부되어야 한다");
            Assert.IsEmpty(claim.Granted);
        });
    }
}
