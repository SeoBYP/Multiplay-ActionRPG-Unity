using Server.Loot;

namespace Server.Tests.Loot;

/// <summary>
/// 드랍 roll 단위 검증 — 확률·수량 범위·미등록 몬스터. rng 주입으로 결정론 검증.
/// </summary>
public class DropTableTests
{
    /// <summary>NextDouble 을 고정 시퀀스로 돌려주는 결정론 Random(확률 임계 검증용).</summary>
    private sealed class StubRandom(double[] doubles, int[] ints) : Random
    {
        private int _di, _ii;
        public override double NextDouble() => doubles[_di++];
        public override int Next(int minValue, int maxValue) => ints[_ii++];
    }

    [Fact]
    public void 확률을_모두_통과하면_모든_후보가_드랍된다()
    {
        // slime: potion_hp_small(0.5), gold_pouch(0.2). NextDouble 0.0 < 둘 다 통과.
        var rng = new StubRandom(doubles: [0.0, 0.0], ints: [3]); // gold_pouch 수량 3
        var drops = DropTable.Roll("slime", rng);

        Assert.Equal(2, drops.Count);
        Assert.Contains(drops, d => d.ItemId == "potion_hp_small" && d.Qty == 1);
        Assert.Contains(drops, d => d.ItemId == "gold_pouch" && d.Qty == 3);
    }

    [Fact]
    public void 확률_임계_이상이면_해당_후보는_드랍되지_않는다()
    {
        // potion(0.5): 0.5 >= 0.5 → 탈락 / gold(0.2): 0.9 >= 0.2 → 탈락.
        var rng = new StubRandom(doubles: [0.5, 0.9], ints: []);
        var drops = DropTable.Roll("slime", rng);

        Assert.Empty(drops);
    }

    [Fact]
    public void MinQty가_MaxQty와_같으면_Next를_호출하지_않고_고정수량이다()
    {
        // potion 만 통과(0.0), gold 탈락(0.9). potion 은 Min==Max==1 → Next 미호출.
        var rng = new StubRandom(doubles: [0.0, 0.9], ints: []);
        var drops = DropTable.Roll("slime", rng);

        Assert.Single(drops);
        Assert.Equal("potion_hp_small", drops[0].ItemId);
        Assert.Equal(1, drops[0].Qty);
    }

    [Fact]
    public void 미등록_몬스터는_빈_드랍이다()
    {
        var rng = new StubRandom(doubles: [0.0], ints: []);
        Assert.Empty(DropTable.Roll("unknown_monster", rng));
        Assert.Empty(DropTable.Roll(null, rng));
    }
}
