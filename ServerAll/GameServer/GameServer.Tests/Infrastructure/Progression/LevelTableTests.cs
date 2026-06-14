using System.IO;
using System.Text;
using Shared.Infrastructure.Progression;

namespace GameServer.Tests.Infrastructure.Progression;

/// <summary>
/// level-table.json(임베디드) 로드 + 룩업/경계 검증.
/// 데이터 진실원 = 클라 LevelTableDefinition SO → Export bake. 여기선 서버 리더 계약만 검증한다.
/// </summary>
public class LevelTableTests
{
    [Fact]
    public void 임베디드_테이블이_60레벨까지_로드된다()
    {
        Assert.Equal(60, LevelTable.MaxLevel);
    }

    [Fact]
    public void Lv1_스탯이_시드값과_일치한다()
    {
        var stats = LevelTable.StatsAt(1);

        Assert.Equal(100, stats.MaxHealth);
        Assert.Equal(50, stats.MaxMana);
        Assert.Equal(10, stats.AttackPower);
        Assert.Equal(5, stats.Defense);
    }

    [Fact]
    public void Lv60_스탯이_거듭제곱_선형성장_시드값과_일치한다()
    {
        var stats = LevelTable.StatsAt(60);

        Assert.Equal(1280, stats.MaxHealth);   // 100 + 59*20
        Assert.Equal(187, stats.AttackPower);  // 10 + 59*3
    }

    [Fact]
    public void ExpToNext_은_레벨이_오를수록_단조증가한다()
    {
        for (int level = 1; level < LevelTable.MaxLevel - 1; level++)
            Assert.True(LevelTable.ExpToNext(level) < LevelTable.ExpToNext(level + 1),
                $"ExpToNext({level}) 가 ExpToNext({level + 1}) 보다 작아야 한다");
    }

    [Fact]
    public void 만렙은_ExpToNext가_무한대라_레벨업_루프가_통과하지_못한다()
    {
        // 만렙(이상)에서는 어떤 누적 Exp 로도 다음 레벨 임계를 넘지 못해야 한다(만렙 고정).
        Assert.Equal(long.MaxValue, LevelTable.ExpToNext(60));
        Assert.Equal(long.MaxValue, LevelTable.ExpToNext(999));
    }

    [Fact]
    public void 범위를_벗어난_레벨은_경계로_clamp된다()
    {
        Assert.Equal(LevelTable.StatsAt(1), LevelTable.StatsAt(0));
        Assert.Equal(LevelTable.StatsAt(1), LevelTable.StatsAt(-5));
        Assert.Equal(LevelTable.StatsAt(60), LevelTable.StatsAt(999));
    }

    [Fact]
    public void 합성_JSON을_파싱한다()
    {
        const string json = """
        {
          "levels": [
            { "level": 1, "expToNext": 100, "maxHealth": 120, "maxMana": 60, "attackPower": 11, "defense": 6, "strength": 7, "dexterity": 8, "intelligence": 9 },
            { "level": 2, "expToNext": 0,   "maxHealth": 140, "maxMana": 70, "attackPower": 14, "defense": 8, "strength": 8, "dexterity": 9, "intelligence": 10 }
          ]
        }
        """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var rows = LevelTable.Parse(stream);

        Assert.Equal(2, rows.Count);
        Assert.Equal(100, rows[1].ExpToNext);
        Assert.Equal(120, rows[1].MaxHealth);
        Assert.Equal(9, rows[1].Intelligence);
        Assert.Equal(0, rows[2].ExpToNext);
    }
}
