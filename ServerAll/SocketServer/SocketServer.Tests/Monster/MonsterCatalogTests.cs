using System.IO;
using System.Text;
using Shared.Infrastructure.Monsters;

namespace Server.Tests.Monster;

/// <summary>
/// monsters.json(임베디드) 로드 + 조회/폴백 검증. 몬스터 정의 단일 소스(스탯+exp).
/// SocketServer Server.Monster.MonsterCatalog 는 이 위에서 시뮬 스탯만 매핑하는 어댑터.
/// </summary>
public class MonsterCatalogTests
{
    [Fact]
    public void 임베디드_slime_정의가_로드된다()
    {
        var slime = MonsterCatalog.Get("slime");

        Assert.Equal(30, slime.MaxHp);
        Assert.Equal(5, slime.AttackDamage);
        Assert.Equal(20, slime.ExpReward); // Main 킬 보상
    }

    [Fact]
    public void test_brute는_exp가_0이다()
    {
        var brute = MonsterCatalog.Get("test_brute");

        Assert.Equal(999999, brute.MaxHp);
        Assert.Equal(0, brute.ExpReward);
    }

    [Fact]
    public void 미등록_몬스터는_Default로_폴백한다()
    {
        Assert.Equal(MonsterCatalog.Default, MonsterCatalog.Get("unknown_monster"));
        Assert.Equal(MonsterCatalog.Default, MonsterCatalog.Get(null));
        Assert.Equal(0, MonsterCatalog.Get("unknown_monster").ExpReward);
    }

    [Fact]
    public void SocketServer_어댑터는_시뮬_스탯을_그대로_매핑한다()
    {
        var stats = global::Server.Monster.MonsterCatalog.Get("slime");
        var def = MonsterCatalog.Get("slime");

        Assert.Equal(def.MaxHp, stats.MaxHp);
        Assert.Equal(def.AttackDamage, stats.AttackDamage);
        Assert.Equal(def.AttackCooldownMs, stats.AttackCooldownMs);
    }

    [Fact]
    public void 합성_JSON을_파싱한다()
    {
        const string json = """
        {
          "monsters": [
            { "monsterId": "goblin", "maxHp": 50, "moveSpeed": 3.0, "aggroRange": 8, "attackRange": 1.5, "attackCooldownMs": 1000, "attackDamage": 8, "expReward": 35 }
          ]
        }
        """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var table = MonsterCatalog.Parse(stream);

        Assert.True(table.ContainsKey("goblin"));
        Assert.Equal(50, table["goblin"].MaxHp);
        Assert.Equal(35, table["goblin"].ExpReward);
    }
}
