using Game.Gameplay.Character;
using NUnit.Framework;
using Script.System.GamePlayAbilitySystem;
using UnityEngine;

namespace Game.Tests.PlayMode.Character
{
    /// <summary>
    /// PlayerStatApplier — 서버 권위 레벨 MaxHealth 를 로컬 ASC Health 에 정렬하는지 검증.
    /// 클라 prefab(100) ↔ 서버 레벨값(140…) desync(다운 후 몬스터 계속 공격) 회귀 가드. Docker 불필요.
    /// </summary>
    [TestFixture]
    public class PlayerStatApplierTests
    {
        private static (GameObject go, GasComponent asc, PlayerStatApplier applier) Make()
        {
            var go = new GameObject("local_player");
            var asc = go.AddComponent<GasComponent>();
            asc.InitializeAttributes();             // 기본 Health 100/100 (prefab 기준선 모사)
            var applier = go.AddComponent<PlayerStatApplier>();
            return (go, asc, applier);
        }

        [Test]
        public void 서버_MaxHealth를_적용하면_ASC_Health가_정렬되고_풀피가_된다()
        {
            var (go, asc, applier) = Make();
            try
            {
                applier.ApplyMaxHealth(140);        // 레벨3 maxHealth 모사

                Assert.IsTrue(asc.Has(EGameplayAttribute.Health));
                Assert.AreEqual(140, asc.Max(EGameplayAttribute.Health), "서버 MaxHealth 로 Max 정렬");
                Assert.AreEqual(140, asc.Current(EGameplayAttribute.Health), "스폰 시 풀피");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void 동일_MaxHealth_재적용은_풀힐하지_않는다()
        {
            var (go, asc, applier) = Make();
            try
            {
                applier.ApplyMaxHealth(140);

                asc.ApplyModifiers(new[]
                {
                    GameplayAttributeModifier.Create(EGameplayAttribute.Health, -40, EModifierType.Additive), // 140→100
                });
                applier.ApplyMaxHealth(140);        // holder 가 킬마다 OnChanged 를 쏴도 변화 없으면 무시

                Assert.AreEqual(100, asc.Current(EGameplayAttribute.Health), "MaxHealth 무변화 재적용은 풀힐 금지(킬마다 풀힐 방지)");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void MaxHealth_0_미갱신이면_prefab_기준선을_유지한다()
        {
            var (go, asc, applier) = Make();
            try
            {
                applier.ApplyMaxHealth(0);          // 스탯 미pull(default) 상태

                Assert.AreEqual(100, asc.Max(EGameplayAttribute.Health), "미갱신(0)이면 prefab 기준선 유지");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
