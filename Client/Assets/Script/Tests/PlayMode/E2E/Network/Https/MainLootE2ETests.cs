using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Game.Gameplay.Loot;
using GameServer.Grpc.Inventory;
using NUnit.Framework;
using Shared.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.E2E
{
    /// <summary>
    /// Main(싱글) 루트 경로 E2E (Docker 서버 대상). 던전 풀 E2E(SocketE2ETests)의 Main 대응판.
    ///
    /// Main 경로의 서버 통신은 GrantItem gRPC 1번뿐이라(나머지는 클라 로컬), 여기서는
    /// **클라 실제 데이터·로직 체인**을 검증한다:
    ///   DropTableDefinition(SO, 클라 저작) → DropTableRoll(서버 던전과 공유 DLL) → GrantItem → 인벤토리 반영.
    /// (MonoBehaviour 글루 — LocalMonster/LocalCombat/LocalGroundItem 입력·콜라이더 — 는 플레이 검증 영역.)
    /// </summary>
    [TestFixture]
    public class MainLootE2ETests : E2ETestBase
    {
        [UnityTest]
        public IEnumerator Main_슬라임_드랍을_굴려_GrantItem하면_인벤토리에_반영된다() => UniTask.ToCoroutine(async () =>
        {
            await RegisterAndLoginAsync(UniqueEmail(), "Test1234!");

            // 1) 클라 저작 SO 로드 — Tools/Loot/Import 로 부트스트랩된 에셋.
            var dropTable = Resources.Load<DropTableDefinition>("Loot/DropTableDefinition");
            Assert.IsNotNull(dropTable, "DropTableDefinition.asset 없음 — Tools/Loot/Import (bootstrap) 필요");

            // 2) SO 데이터 → 공유 roll(서버 던전과 동일 DropTableRoll). slime potion 은 보장 드랍(1.0).
            var entries = new List<DropEntry>();
            foreach (var d in dropTable.Get("slime"))
                entries.Add(new DropEntry(d.itemId, d.chance, d.minQty, d.maxQty));
            Assert.IsNotEmpty(entries, "slime 드랍 데이터가 비어있다");

            var drops = DropTableRoll.Roll(entries, new global::System.Random(12345));
            Assert.IsTrue(drops.Any(x => x.ItemId == "potion_hp_small"), "보장 드랍 potion_hp_small 누락");

            // 3) 줍기 확정 시 클라가 하는 것과 동일: 굴린 각 드랍을 GrantItem 으로 지급.
            foreach (var drop in drops)
            {
                var res = await InventoryService.GrantItemAsync(
                    new GrantItemRequest { ItemId = drop.ItemId, Qty = drop.Qty }, Timeout());
                Assert.IsTrue(res.Result.Success, $"{drop.ItemId} 지급 실패: {res.Result.Message}");
            }

            // 4) 진실원 = 서버 DB: 굴린 드랍이 인벤토리에 반영됐는지(수량 누적).
            var inv = await InventoryService.GetInventoryAsync(new GetInventoryRequest(), Timeout());
            foreach (var drop in drops)
            {
                var item = inv.Items.FirstOrDefault(i => i.ItemId == drop.ItemId);
                Assert.IsNotNull(item, $"{drop.ItemId} 가 인벤토리에 반영되지 않았다");
                Assert.GreaterOrEqual(item.Quantity, drop.Qty, $"{drop.ItemId} 수량 부족");
            }
        });
    }
}
