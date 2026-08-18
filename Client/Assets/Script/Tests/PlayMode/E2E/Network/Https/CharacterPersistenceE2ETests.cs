using System.Collections;
using System.Linq;
using Cysharp.Threading.Tasks;
using GameServer.Grpc.Auth;
using GameServer.Grpc.Inventory;
using GameServer.Grpc.Progression;
using GameServer.Grpc.Wallet;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.E2E
{
    /// <summary>
    /// 6.1 캐릭터 진행 영속 합류 — 로그아웃→재로그인(새 토큰/세션) 후에도 진행 상태가 보존되는지 검증(Docker 서버).
    ///
    /// 각 도메인은 cache-aside(Redis MISS → DB)라 새 세션도 DB에서 복원된다. 여기선 **재접속 전체 흐름**으로
    /// 진행(Exp)·인벤토리·지갑(골드)이 동일하게 남는지 확인한다(셋 다 게임플레이로 결정적 변동 가능).
    ///   - 장비(user_equipments) 영속은 동일 cache-aside 패턴이며 EquipmentRepositoryIntegrationTests 가
    ///     MISS→DB 복원을 직접 검증(E2E 에선 장비 아이템을 결정적으로 획득하기 어려워 제외).
    /// </summary>
    [TestFixture]
    public class CharacterPersistenceE2ETests : E2ETestBase
    {
        private const string MainMap = "main_field_01";
        private const int Potion = 1001;

        [UnityTest]
        public IEnumerator 재접속_후_레벨_경험치_인벤토리_골드가_보존된다() => UniTask.ToCoroutine(async () =>
        {
            var email = UniqueEmail();
            await RegisterAndLoginAsync(email, "Test1234!");

            // 진행 상태 변경: exp(ClaimMonsterExp) + 인벤/골드(ClaimKill = potion 보장 + gold 항상드랍).
            var exp = await InventoryService.ClaimMonsterExpAsync(
                new ClaimMonsterExpRequest { MapId = MainMap, SlotId = 3 }, Timeout());
            Assert.IsTrue(exp.Result.Success, exp.Result.Message);

            var k1 = await InventoryService.ClaimKillAsync(new ClaimKillRequest { MapId = MainMap, SlotId = 1 }, Timeout());
            Assert.IsTrue(k1.Result.Success, k1.Result.Message);
            var k2 = await InventoryService.ClaimKillAsync(new ClaimKillRequest { MapId = MainMap, SlotId = 2 }, Timeout());
            Assert.IsTrue(k2.Result.Success, k2.Result.Message);

            // 로그아웃 전 스냅샷(진실원 = 서버 DB).
            var progBefore = await ProgressionService.GetProgressionAsync(new GetProgressionRequest(), Timeout());
            var invBefore = await InventoryService.GetInventoryAsync(new GetInventoryRequest(), Timeout());
            var walletBefore = await WalletService.GetWalletAsync(new GetWalletRequest(), Timeout());

            int potionBefore = invBefore.Items.FirstOrDefault(i => i.ItemId == Potion)?.Quantity ?? 0;
            // 보상 수치를 하드코딩하지 않는다 — expReward 는 저작 데이터(MonsterCatalogDefinition→monsters.json)라
            // 몬스터를 교체·리밸런스하면 바뀐다. (실제로 slime→creepy_demon 교체로 20→18 이 되면서 이 단언이 깨져 있었다.)
            // 이 테스트가 지켜야 할 것은 "재접속해도 값이 보존된다"이지 "그 값이 20이다"가 아니다.
            Assert.Greater(exp.ExpGained, 0, "사전 조건: ClaimMonsterExp 가 실제로 exp 를 지급해야 한다");
            Assert.AreEqual(exp.ExpGained, progBefore.Exp, "사전 조건: 지급된 exp 가 진행 상태에 반영돼야 한다");
            Assert.GreaterOrEqual(potionBefore, 1, "사전 조건: potion 보장 드랍");
            Assert.Greater(walletBefore.Balance, 0, "사전 조건: 골드 항상 드랍");

            // ── 재접속: 로그아웃 → 새 로그인(새 토큰/세션) ──
            var logout = await AuthService.LogoutAsync(new LogoutRequest(), Timeout());
            Assert.IsTrue(logout.Result.Success, logout.Result.Message);
            AccessToken = null;
            RefreshToken = null;
            SessionId = null;
            await LoginAsync(email, "Test1234!");

            // 재접속 후: 전부 보존(새 세션이 DB 에서 cache-aside 복원).
            var progAfter = await ProgressionService.GetProgressionAsync(new GetProgressionRequest(), Timeout());
            var invAfter = await InventoryService.GetInventoryAsync(new GetInventoryRequest(), Timeout());
            var walletAfter = await WalletService.GetWalletAsync(new GetWalletRequest(), Timeout());

            int potionAfter = invAfter.Items.FirstOrDefault(i => i.ItemId == Potion)?.Quantity ?? 0;
            Assert.AreEqual(progBefore.Level, progAfter.Level, "레벨 보존");
            Assert.AreEqual(progBefore.Exp, progAfter.Exp, "Exp 보존");
            Assert.AreEqual(potionBefore, potionAfter, "인벤토리 수량 보존");
            Assert.AreEqual(walletBefore.Balance, walletAfter.Balance, "골드 잔액 보존");
        });
    }
}
