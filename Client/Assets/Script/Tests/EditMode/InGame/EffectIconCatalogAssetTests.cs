using Game.Presentation.InGame;
using NUnit.Framework;
using Script.System.GamePlayAbilitySystem;
using UnityEngine;

namespace Game.Tests.EditMode.InGame
{
    /// <summary>
    /// 기본 EffectIconCatalog 에셋이 Resources에서 로드되고 카테고리→Sprite가 실제로 매핑됐는지 검증.
    /// (PSD 멀티스프라이트 서브에셋 참조가 깨지면 여기서 잡힌다)
    /// </summary>
    public class EffectIconCatalogAssetTests
    {
        [Test]
        public void 기본_카탈로그가_Resources에서_로드되고_3카테고리_아이콘이_매핑된다()
        {
            var catalog = Resources.Load<EffectIconCatalog>("Effects/EffectIconCatalog");
            Assert.IsNotNull(catalog, "Resources/Effects/EffectIconCatalog 로드 실패");

            Assert.IsNotNull(catalog.GetIcon(EEffectCategory.AttackPower), "AttackPower 아이콘 미매핑");
            Assert.IsNotNull(catalog.GetIcon(EEffectCategory.Defense), "Defense 아이콘 미매핑");
            Assert.IsNotNull(catalog.GetIcon(EEffectCategory.MoveSpeed), "MoveSpeed 아이콘 미매핑");
        }

        [Test]
        public void 버프색과_디버프색이_서로_다르게_설정돼_있다()
        {
            var catalog = Resources.Load<EffectIconCatalog>("Effects/EffectIconCatalog");
            Assert.IsNotNull(catalog);
            Assert.AreNotEqual(catalog.GetColor(true), catalog.GetColor(false));
        }
    }
}
