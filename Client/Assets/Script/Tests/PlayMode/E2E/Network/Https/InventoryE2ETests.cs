using System.Collections;
using Cysharp.Threading.Tasks;
using GameServer.Grpc.Inventory;
using Grpc.Core;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.E2E
{
    /// <summary>
    /// 인벤토리 gRPC E2E (Docker 서버 대상). 아이템 지급은 내부(보상/루트)라 RPC 없음 →
    /// 신규 유저 조회(빈 인벤토리 + 인증 경로)를 검증한다.
    /// </summary>
    [TestFixture]
    public class InventoryE2ETests : E2ETestBase
    {
        [UnityTest]
        public IEnumerator GetInventory_신규_유저는_성공하고_빈_목록() => UniTask.ToCoroutine(async () =>
        {
            await RegisterAndLoginAsync(UniqueEmail(), "Test1234!");

            var response = await InventoryService.GetInventoryAsync(new GetInventoryRequest(), Timeout());

            Assert.IsNotNull(response);
            Assert.IsTrue(response.Result.Success, response.Result.Message);
            Assert.AreEqual(0, response.Items.Count);
        });

        [UnityTest]
        public IEnumerator GetInventory_미인증_호출은_거부된다() => UniTask.ToCoroutine(async () =>
        {
            // 로그인하지 않음 → AccessToken 없음 → AuthInterceptor가 Unauthenticated로 거부.
            RpcException caught = null;
            try
            {
                await InventoryService.GetInventoryAsync(new GetInventoryRequest(), Timeout());
            }
            catch (RpcException e)
            {
                caught = e;
            }

            Assert.IsNotNull(caught, "미인증 호출인데 거부되지 않았다");
            Assert.AreEqual(StatusCode.Unauthenticated, caught.StatusCode);
        });

        [UnityTest]
        public IEnumerator GrantItem_정상_지급은_성공하고_보유수량을_반환한다() => UniTask.ToCoroutine(async () =>
        {
            await RegisterAndLoginAsync(UniqueEmail(), "Test1234!");

            var grant = await InventoryService.GrantItemAsync(
                new GrantItemRequest { ItemId = "potion_hp_small", Qty = 3 }, Timeout());

            Assert.IsTrue(grant.Result.Success, grant.Result.Message);
            Assert.AreEqual(3, grant.NewQuantity);

            // 진실원 = 서버 DB: GetInventory 로 재확인.
            var inv = await InventoryService.GetInventoryAsync(new GetInventoryRequest(), Timeout());
            Assert.AreEqual(1, inv.Items.Count);
            Assert.AreEqual("potion_hp_small", inv.Items[0].ItemId);
            Assert.AreEqual(3, inv.Items[0].Quantity);
        });

        [UnityTest]
        public IEnumerator GrantItem_수량상한_초과는_거부되고_지급되지_않는다() => UniTask.ToCoroutine(async () =>
        {
            await RegisterAndLoginAsync(UniqueEmail(), "Test1234!");

            // MaxGrantPerCall(99) 초과 → 서버 가드가 거부(GrantItemAsync 미호출).
            var grant = await InventoryService.GrantItemAsync(
                new GrantItemRequest { ItemId = "potion_hp_small", Qty = 9999 }, Timeout());

            Assert.IsFalse(grant.Result.Success);

            var inv = await InventoryService.GetInventoryAsync(new GetInventoryRequest(), Timeout());
            Assert.AreEqual(0, inv.Items.Count, "거부됐는데 지급됐다");
        });

        [UnityTest]
        public IEnumerator GrantItem_미존재_itemId는_거부된다() => UniTask.ToCoroutine(async () =>
        {
            await RegisterAndLoginAsync(UniqueEmail(), "Test1234!");

            var grant = await InventoryService.GrantItemAsync(
                new GrantItemRequest { ItemId = "hack_sword_9000", Qty = 1 }, Timeout());

            Assert.IsFalse(grant.Result.Success);

            var inv = await InventoryService.GetInventoryAsync(new GetInventoryRequest(), Timeout());
            Assert.AreEqual(0, inv.Items.Count);
        });

        [UnityTest]
        public IEnumerator GrantItem_미인증_호출은_거부된다() => UniTask.ToCoroutine(async () =>
        {
            // NUnit 은 fixture 인스턴스를 테스트 간 공유 → 앞선 로그인 테스트의 AccessToken 이 남아 있을 수 있다.
            // 미인증 경로를 검증하려면 토큰을 명시적으로 비운다.
            AccessToken = null;

            RpcException caught = null;
            try
            {
                await InventoryService.GrantItemAsync(
                    new GrantItemRequest { ItemId = "potion_hp_small", Qty = 1 }, Timeout());
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
