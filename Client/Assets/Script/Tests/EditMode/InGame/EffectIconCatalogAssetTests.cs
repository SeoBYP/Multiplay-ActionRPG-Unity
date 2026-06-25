using Game.Presentation.InGame;
using NUnit.Framework;
using Script.System.GamePlayAbilitySystem;
using UnityEngine.AddressableAssets;

namespace Game.Tests.EditMode.InGame
{
    /// <summary>
    /// 기본 EffectIconCatalog 에셋이 Addressables에서 로드되고 카테고리→Sprite가 실제로 매핑됐는지 검증.
    /// (Resources 폐기 — 주소 = 에셋 경로. PSD 멀티스프라이트 서브에셋 참조가 깨지면 여기서 잡힌다)
    /// </summary>
    public class EffectIconCatalogAssetTests
    {
        private const string Address = "Assets/GameData/Effects/EffectIconCatalog.asset";

        private static EffectIconCatalog Load()
            => Addressables.LoadAssetAsync<EffectIconCatalog>(Address).WaitForCompletion();

        [Test]
        public void 기본_카탈로그가_Addressables에서_로드되고_3카테고리_아이콘이_매핑된다()
        {
            var catalog = Load();
            Assert.IsNotNull(catalog, $"Addressables {Address} 로드 실패");

            Assert.IsNotNull(catalog.GetIcon(EEffectCategory.AttackPower), "AttackPower 아이콘 미매핑");
            Assert.IsNotNull(catalog.GetIcon(EEffectCategory.Defense), "Defense 아이콘 미매핑");
            Assert.IsNotNull(catalog.GetIcon(EEffectCategory.MoveSpeed), "MoveSpeed 아이콘 미매핑");
        }

        [Test]
        public void 버프색과_디버프색이_서로_다르게_설정돼_있다()
        {
            var catalog = Load();
            Assert.IsNotNull(catalog);
            Assert.AreNotEqual(catalog.GetColor(true), catalog.GetColor(false));
        }
    }
}
