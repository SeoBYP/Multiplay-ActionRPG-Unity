using System;
using System.Collections.Generic;

namespace Shared.Gameplay.Tests;

/// <summary>
/// 드랍 roll 순수 로직 검증 — 확률·수량 범위. 데이터(entries)는 외부 주입이므로 여기선 로직만 본다.
/// (데이터 파싱은 Shared.Infrastructure 의 DropTableCatalog 테스트가 담당.)
/// rng 주입으로 결정론 검증.
/// </summary>
public class DropTableRollTests
{
    /// <summary>NextDouble 을 고정 시퀀스로 돌려주는 결정론 Random(확률 임계 검증용).</summary>
    private sealed class StubRandom(double[] doubles, int[] ints) : Random
    {
        private int _di, _ii;
        public override double NextDouble() => doubles[_di++];
        public override int Next(int minValue, int maxValue) => ints[_ii++];
    }

    // slime 후보: potion_hp_small(보장 1.0), gold(0.2, 1~3)
    private static readonly List<DropEntry> Slime = new()
    {
        new DropEntry("potion_hp_small", 1.0, 1, 1),
        new DropEntry("gold", 0.2, 1, 3),
    };

    [Fact]
    public void 확률을_모두_통과하면_모든_후보가_드랍된다()
    {
        var rng = new StubRandom(doubles: [0.0, 0.0], ints: [3]); // gold 수량 3
        var drops = DropTableRoll.Roll(Slime, rng);

        Assert.Equal(2, drops.Count);
        Assert.Contains(drops, d => d.ItemId == "potion_hp_small" && d.Qty == 1);
        Assert.Contains(drops, d => d.ItemId == "gold" && d.Qty == 3);
    }

    [Fact]
    public void 확률_임계_이상이면_해당_후보는_드랍되지_않는다()
    {
        // potion(1.0): NextDouble 은 항상 < 1.0 → 보장 / gold(0.2): 0.9 >= 0.2 → 탈락.
        var rng = new StubRandom(doubles: [0.99, 0.9], ints: []);
        var drops = DropTableRoll.Roll(Slime, rng);

        Assert.Single(drops);
        Assert.Equal("potion_hp_small", drops[0].ItemId);
    }

    [Fact]
    public void Chance_1_0_후보는_확률과_무관하게_항상_드랍된다()
    {
        var rng = new StubRandom(doubles: [0.999999, 0.999999], ints: []);
        var drops = DropTableRoll.Roll(Slime, rng);

        Assert.Contains(drops, d => d.ItemId == "potion_hp_small" && d.Qty == 1);
        Assert.DoesNotContain(drops, d => d.ItemId == "gold");
    }

    [Fact]
    public void MinQty가_MaxQty와_같으면_Next를_호출하지_않고_고정수량이다()
    {
        // potion 만 통과(0.0), gold 탈락(0.9). potion 은 Min==Max==1 → Next 미호출(ints 비어도 안전).
        var rng = new StubRandom(doubles: [0.0, 0.9], ints: []);
        var drops = DropTableRoll.Roll(Slime, rng);

        Assert.Single(drops);
        Assert.Equal("potion_hp_small", drops[0].ItemId);
        Assert.Equal(1, drops[0].Qty);
    }

    [Fact]
    public void 빈_목록이나_null이면_빈_드랍이다()
    {
        var rng = new StubRandom(doubles: [0.0], ints: []);
        Assert.Empty(DropTableRoll.Roll(new List<DropEntry>(), rng));
        Assert.Empty(DropTableRoll.Roll(null, rng));
    }
}
