using Shared.Gameplay.Items;
using Shared.Infrastructure.Items;

namespace Server.Tests.Items;

/// <summary>
/// numericId 불변식 — ItemId 를 string 에서 int 로 옮기는 전환(1단계)의 회귀 가드.
///
/// <para>저작 시점 검증은 클라 <c>ItemCatalogExporter</c> 가 하지만(bake 거부), 그건 사람이 Export 를
/// 돌려야 작동한다. 서버 테스트로 한 번 더 잠가 <b>어긋난 bake 가 커밋되는 것</b>을 막는다 —
/// items.json 이 Exporter 없이 손으로 저작되던 시절 클라와 갈라졌던 전례가 있다(A4).</para>
/// </summary>
public class ItemNumericIdTests
{
    [Fact]
    public void 모든_아이템이_numericId를_갖는다()
    {
        // 0 은 "미배정"이다. 하나라도 0 이면 int 전환 시 서로 충돌한다.
        Assert.All(ItemCatalogData.Current.Items,
            i => Assert.True(i.NumericId > 0, $"'{i.ItemId}' 에 numericId 가 없다."));
    }

    [Fact]
    public void numericId는_중복되지_않는다()
    {
        // DB 복합 PK(UserId, ItemId)·패킷 키가 될 값이라 겹치면 조용히 덮어써진다.
        var items = ItemCatalogData.Current.Items;
        Assert.Equal(items.Count, ItemCatalogData.Current.ItemsByNumericId.Count);
    }

    [Theory]
    [InlineData(ShopCategory.Potion, 1000, 1999)]
    [InlineData(ShopCategory.Weapon, 2100, 2199)]
    [InlineData(ShopCategory.Armor, 2200, 2299)]
    [InlineData(ShopCategory.Accessory, 2300, 2399)]
    public void numericId_대역이_분류와_일치한다(ShopCategory category, int lo, int hi)
    {
        // 대역이 곧 분류다 — 문자열 id 를 걷어낸 뒤(2단계) 로그·DB 만 보고 무엇인지 알 수 있는 유일한 단서.
        foreach (var shop in ItemCatalogData.Current.Shop)
        {
            if (shop.Category != category) continue;
            var def = ItemCatalogData.Current.ItemsById[shop.ItemId];
            Assert.InRange(def.NumericId, lo, hi);
        }
    }

    [Fact]
    public void numericId로_조회하면_itemId_조회와_같은_정의를_준다()
    {
        foreach (var i in ItemCatalogData.Current.Items)
            Assert.Same(i, ItemCatalogData.Current.ItemsByNumericId[i.NumericId]);
    }
}
