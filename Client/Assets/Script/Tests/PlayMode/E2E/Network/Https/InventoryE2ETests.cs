using System.Collections;
using Cysharp.Threading.Tasks;
using GameServer.Grpc.Inventory;
using Grpc.Core;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.E2E
{
    /// <summary>
    /// 인벤토리 gRPC E2E (Docker 서버 대상). 조회(빈 인벤토리 + 인증 경로)를 검증한다.
    /// 아이템 획득(Main ClaimKill / 던전 루트)·소비는 각 경로 E2E(MainLootE2ETests/SocketE2ETests)가 검증한다.
    /// ※ 구 GrantItem(itemId,qty) gRPC 는 무한파밍 핵으로 제거됨 → ClaimKill(서버 검증·roll)로 대체.
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
    }
}
