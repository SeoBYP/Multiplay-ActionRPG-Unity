using System;
using System.IO;
using System.Text;
using Shared.Gameplay;
using Shared.Infrastructure.Loot;

namespace Server.Tests.Loot;

/// <summary>
/// drop-tables.json(임베디드) 파싱 + 카탈로그 조회·roll 위임 검증.
/// roll 확률 로직 자체는 Shared.Gameplay.Tests.DropTableRollTests 가 담당 — 여기선 데이터/파싱/위임만.
/// </summary>
public class DropTableCatalogTests
{
    private sealed class StubRandom(double[] doubles, int[] ints) : Random
    {
        private int _di, _ii;
        public override double NextDouble() => doubles[_di++];
        public override int Next(int minValue, int maxValue) => ints[_ii++];
    }

    [Fact]
    public void 임베디드_slime_데이터가_로드된다()
    {
        var entries = DropTableCatalog.Get("slime");

        Assert.Contains(entries, e => e.ItemId == "potion_hp_small" && e.Chance == 1.0 && e.MinQty == 1 && e.MaxQty == 1);
        // chance 는 SO(float) → JSON 직렬화로 0.2f→0.20000000298.. 의 부동소수 아티팩트가 끼므로 근사 비교한다.
        Assert.Contains(entries, e => e.ItemId == "gold" && Math.Abs(e.Chance - 0.2) < 1e-6 && e.MinQty == 1 && e.MaxQty == 3);
    }

    [Fact]
    public void 임베디드_goblin_데이터가_로드된다()
    {
        // SO(DropTableDefinition) 저작 → Tools/Loot/Export 로 추가된 데이터가 임베디드 JSON 에 반영됐는지 검증.
        var entries = DropTableCatalog.Get("goblin");

        Assert.Single(entries);
        Assert.Contains(entries, e => e.ItemId == "potion_hp_small" && e.Chance == 1.0 && e.MinQty == 5 && e.MaxQty == 10);
    }

    [Fact]
    public void 미등록_몬스터는_빈_목록이다()
    {
        Assert.Empty(DropTableCatalog.Get("unknown_monster"));
        Assert.Empty(DropTableCatalog.Get(null));
    }

    [Fact]
    public void Roll은_카탈로그_조회후_DropTableRoll에_위임한다()
    {
        // slime 드랍 = potion(보장 1.0) + gold + 장비 8종. potion 만 통과(0.0<1.0), 나머지 9개는 0.9 로 전부 탈락.
        // (drops 순서대로 후보당 NextDouble 1회 소비 → 10개 제공.)
        var rng = new StubRandom(doubles: [0.0, 0.9, 0.9, 0.9, 0.9, 0.9, 0.9, 0.9, 0.9, 0.9], ints: []);
        var drops = DropTableCatalog.Roll("slime", rng);

        Assert.Single(drops);
        Assert.Equal("potion_hp_small", drops[0].ItemId);
    }

    [Fact]
    public void 미등록_몬스터_Roll은_빈_드랍이다()
    {
        var rng = new StubRandom(doubles: [0.0], ints: []);
        Assert.Empty(DropTableCatalog.Roll("unknown_monster", rng));
    }

    [Fact]
    public void 합성_JSON을_파싱한다()
    {
        const string json = """
        {
          "tables": [
            { "monsterId": "goblin", "drops": [
              { "itemId": "coin", "chance": 0.75, "minQty": 2, "maxQty": 5 }
            ] }
          ]
        }
        """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var tables = DropTableCatalog.Parse(stream);

        Assert.True(tables.ContainsKey("goblin"));
        var goblin = tables["goblin"];
        Assert.Single(goblin);
        Assert.Equal("coin", goblin[0].ItemId);
        Assert.Equal(0.75, goblin[0].Chance);
        Assert.Equal(2, goblin[0].MinQty);
        Assert.Equal(5, goblin[0].MaxQty);
    }
}
