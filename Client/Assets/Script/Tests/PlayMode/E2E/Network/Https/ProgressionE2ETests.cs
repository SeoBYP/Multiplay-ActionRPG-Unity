using System.Collections;
using Cysharp.Threading.Tasks;
using GameServer.Grpc.Progression;
using Grpc.Core;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.E2E
{
    /// <summary>
    /// 진행/스탯 gRPC E2E (Docker 서버 대상). 클라 GetProgression → 서버 progression+LevelTable 합성 전 경로 검증(2.3).
    /// 신규 유저는 적립 이력이 없어 기본 Lv1/Exp0 + Lv1 테이블 스탯을 받아야 한다.
    /// 레벨업(Exp 적립) 자체는 서버 던전보상 경로(GameServer DungeonResultRewardE2ETests)가 검증한다.
    /// </summary>
    [TestFixture]
    public class ProgressionE2ETests : E2ETestBase
    {
        [UnityTest]
        public IEnumerator GetProgression_신규_유저는_Lv1_기본스탯을_반환한다() => UniTask.ToCoroutine(async () =>
        {
            await RegisterAndLoginAsync(UniqueEmail(), "Test1234!");

            var res = await ProgressionService.GetProgressionAsync(new GetProgressionRequest(), Timeout());

            Assert.IsNotNull(res);
            Assert.IsTrue(res.Result.Success, res.Result.Message);
            Assert.AreEqual(1, res.Level, "신규 유저는 Lv1");
            Assert.AreEqual(0, res.Exp);
            Assert.AreEqual(60, res.MaxLevel);
            Assert.AreEqual(100, res.ExpToNext, "Lv1 → 다음 레벨까지 100");
            Assert.IsNotNull(res.Stats);
            Assert.AreEqual(100, res.Stats.MaxHealth, "Lv1 MaxHealth");
            Assert.AreEqual(10, res.Stats.AttackPower, "Lv1 AttackPower");
            Assert.AreEqual(5, res.Stats.Defense, "Lv1 Defense");
        });

        [UnityTest]
        public IEnumerator GetProgression_미인증_호출은_거부된다() => UniTask.ToCoroutine(async () =>
        {
            // 로그인하지 않음 → AccessToken 없음 → AuthInterceptor가 Unauthenticated로 거부.
            RpcException caught = null;
            try
            {
                await ProgressionService.GetProgressionAsync(new GetProgressionRequest(), Timeout());
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
