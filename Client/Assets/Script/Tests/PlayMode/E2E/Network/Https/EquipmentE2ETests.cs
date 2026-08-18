using System.Collections;
using Cysharp.Threading.Tasks;
using GameServer.Grpc.Equipment;
using Grpc.Core;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.E2E
{
    /// <summary>
    /// 장비 gRPC E2E (Docker 서버 대상, 3.2). 실제 클라 gRPC 채널 → EquipmentService 계약/인증/검증 경로를 관통한다.
    ///
    /// ※ 장착 happy-path(보유 장비 착용)는 공개 API 로 장비를 소유시킬 경로가 없어(GrantItem 제거·드랍 테이블에 장비 없음)
    ///    서버 단위 EquipmentGrpcServiceTests 가 담당. 여기선 미보유/미지정/빈조회/인증 게이트 = 클라→Docker 관통.
    /// </summary>
    [TestFixture]
    public class EquipmentE2ETests : E2ETestBase
    {
        [UnityTest]
        public IEnumerator GetEquipment_신규_유저는_성공하고_빈_목록() => UniTask.ToCoroutine(async () =>
        {
            await RegisterAndLoginAsync(UniqueEmail(), "Test1234!");

            var response = await EquipmentService.GetEquipmentAsync(new GetEquipmentRequest(), Timeout());

            Assert.IsNotNull(response);
            Assert.IsTrue(response.Result.Success, response.Result.Message);
            Assert.AreEqual(0, response.Items.Count);
        });

        [UnityTest]
        public IEnumerator GetEquipment_미인증_호출은_거부된다() => UniTask.ToCoroutine(async () =>
        {
            RpcException caught = null;
            try
            {
                await EquipmentService.GetEquipmentAsync(new GetEquipmentRequest(), Timeout());
            }
            catch (RpcException e)
            {
                caught = e;
            }

            Assert.IsNotNull(caught, "미인증 호출인데 거부되지 않았다");
            Assert.AreEqual(StatusCode.Unauthenticated, caught.StatusCode);
        });

        [UnityTest]
        public IEnumerator Equip_미보유_장비는_실패한다() => UniTask.ToCoroutine(async () =>
        {
            await RegisterAndLoginAsync(UniqueEmail(), "Test1234!");

            // 보유하지 않은 장비 → 서버가 소유 검증으로 거부(Result.Success=false, 예외 아님).
            var response = await EquipmentService.EquipAsync(new EquipRequest { ItemId = 2101 }, Timeout());

            Assert.IsNotNull(response);
            Assert.IsFalse(response.Result.Success, "미보유 장비인데 장착이 성공했다");
        });

        [UnityTest]
        public IEnumerator Equip_미인증_호출은_거부된다() => UniTask.ToCoroutine(async () =>
        {
            RpcException caught = null;
            try
            {
                await EquipmentService.EquipAsync(new EquipRequest { ItemId = 2101 }, Timeout());
            }
            catch (RpcException e)
            {
                caught = e;
            }

            Assert.IsNotNull(caught, "미인증 호출인데 거부되지 않았다");
            Assert.AreEqual(StatusCode.Unauthenticated, caught.StatusCode);
        });

        [UnityTest]
        public IEnumerator Unequip_빈_슬롯은_멱등하게_성공한다() => UniTask.ToCoroutine(async () =>
        {
            await RegisterAndLoginAsync(UniqueEmail(), "Test1234!");

            // 착용한 적 없는 슬롯 해제 → 멱등(성공). 이후 조회는 여전히 빈 목록.
            var response = await EquipmentService.UnequipAsync(
                new UnequipRequest { Slot = EquipmentType.Weapon }, Timeout());

            Assert.IsTrue(response.Result.Success, response.Result.Message);

            var get = await EquipmentService.GetEquipmentAsync(new GetEquipmentRequest(), Timeout());
            Assert.AreEqual(0, get.Items.Count);
        });

        [UnityTest]
        public IEnumerator Unequip_미지정_슬롯은_실패한다() => UniTask.ToCoroutine(async () =>
        {
            await RegisterAndLoginAsync(UniqueEmail(), "Test1234!");

            // UNSPECIFIED(0) 슬롯은 서버가 거부(알 수 없는 슬롯).
            var response = await EquipmentService.UnequipAsync(
                new UnequipRequest { Slot = EquipmentType.Unspecified }, Timeout());

            Assert.IsNotNull(response);
            Assert.IsFalse(response.Result.Success, "미지정 슬롯인데 해제가 성공했다");
        });
    }
}
