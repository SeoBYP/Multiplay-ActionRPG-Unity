using Shared.Gameplay;

namespace Server.Tests.Monster;

/// <summary>
/// AC-E4: 드롭 롤의 레벨·등급 반영. 설계 = docs/wiki/monster-leveling.md §4.3.
///
/// 배율은 <b>인자로 들어온다</b> — <c>DropTableRoll</c> 은 Shared.Gameplay(순수)라 플레이어 곡선을 모른다.
/// 여기선 그 순수 계약을 고정한다(배율 계산 자체는 MonsterLevelScalingTests).
/// </summary>
public class DropLevelTierTests
{
    /// <summary>항상 같은 값을 내는 rng — 확률 판정을 결정론으로 만든다.</summary>
    private sealed class FixedRng : Random
    {
        private readonly double _value;
        public FixedRng(double value) => _value = value;
        protected override double Sample() => _value;
        public override double NextDouble() => _value;
        public override int Next(int minValue, int maxValue) => minValue; // 수량은 항상 하한
    }

    private static readonly DropEntry Gold = new("gold", chance: 0.5, minQty: 10, maxQty: 30);
    private static readonly DropEntry Sword = new("sword_basic", chance: 0.5, minQty: 1, maxQty: 1);

    [Fact]
    public void 확률은_1을_넘지_않는다()
    {
        // 배율이 커도 "확정 이상"은 없다 — rng 가 1.0 이면 어떤 배율이어도 미적중이어야 정의가 일관된다.
        var rng = new FixedRng(1.0);
        Assert.Empty(DropTableRoll.Roll(new[] { Gold }, rng, chanceMultiplier: 100.0));
    }

    [Fact]
    public void 레벨_수량배율은_가변수량에만_걸린다()
    {
        // gold(10~30)는 스케일, 장비(1~1)는 그대로 — 배율을 걸면 검이 2자루 떨어진다.
        var rng = new FixedRng(0.0); // 항상 적중, 수량은 하한(gold=10, sword=1)

        var results = DropTableRoll.Roll(new[] { Gold, Sword }, rng, quantityMultiplier: 2.0);

        Assert.Equal(20, results.Single(r => r.ItemId == "gold").Qty);        // 10 × 2
        Assert.Equal(1, results.Single(r => r.ItemId == "sword_basic").Qty);  // 1 (스케일 제외)
    }

    [Fact]
    public void 배율_기본값은_기존_동작과_같다()
    {
        // E4 는 동작 보존이어야 한다 — 배율을 안 주면 예전 결과 그대로.
        var rng = new FixedRng(0.0);

        var legacy = DropTableRoll.Roll(new[] { Gold, Sword }, rng);
        var scaled = DropTableRoll.Roll(new[] { Gold, Sword }, rng, 1.0, 1.0);

        Assert.Equal(legacy.Count, scaled.Count);
        Assert.Equal(legacy[0].Qty, scaled[0].Qty);
    }

    [Fact]
    public void 미등록_몬스터는_굴려도_아무것도_안_나온다()
    {
        var rng = new FixedRng(0.0);
        var entries = Shared.Infrastructure.Loot.DropTableCatalog.Get("no_such_monster");

        Assert.Empty(DropTableRoll.Roll(entries, rng));
    }

    [Fact]
    public void 로스터_전원이_드롭테이블을_갖는다_E5()
    {
        // E5 이전엔 9마리 중 creepy_demon 만 있었다(7마리가 아무것도 안 떨굼).
        // test_brute 는 테스트 픽스처라 의도적 제외 — 드롭이 붙으면 E2E 기대값이 오염된다.
        string[] roster =
        {
            "vampire_bat", "creepy_demon", "arachnya", "demon_girl",
            "wild_centaur", "gargoyle", "undead_axemaster", "leviathan",
        };

        foreach (var id in roster)
            Assert.NotEmpty(Shared.Infrastructure.Loot.DropTableCatalog.Get(id));

        Assert.Empty(Shared.Infrastructure.Loot.DropTableCatalog.Get("test_brute"));
    }

    [Fact]
    public void 유령_테이블_goblin_이_제거됐다_E5()
    {
        // goblin 은 monsters.json 에 없어 스폰될 수 없는데 drop-tables.json 에만 남아 있던 유령이었다.
        Assert.Empty(Shared.Infrastructure.Loot.DropTableCatalog.Get("goblin"));
    }

    [Fact]
    public void 모든_드롭_엔트리가_유효하다_E5()
    {
        // 손으로 만든 JSON 이 서버 스키마로 제대로 파싱되는지 — 값이 깨지면 여기서 드러난다.
        string[] roster =
        {
            "vampire_bat", "creepy_demon", "arachnya", "demon_girl",
            "wild_centaur", "gargoyle", "undead_axemaster", "leviathan",
        };

        foreach (var id in roster)
        {
            foreach (var e in Shared.Infrastructure.Loot.DropTableCatalog.Get(id))
            {
                Assert.False(string.IsNullOrWhiteSpace(e.ItemId), $"{id}: itemId 가 비었다");
                Assert.InRange(e.Chance, 0.0001, 1.0);
                Assert.InRange(e.MinQty, 1, e.MaxQty);
            }
        }
    }
}
