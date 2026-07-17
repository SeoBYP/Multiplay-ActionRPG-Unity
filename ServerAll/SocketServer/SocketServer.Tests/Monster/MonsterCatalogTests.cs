using System.IO;
using System.Text;
using Shared.Infrastructure.Monsters;

namespace Server.Tests.Monster;

/// <summary>
/// monsters.json(임베디드) 로드 + 조회/폴백 검증. 몬스터 정의 단일 소스("무엇인가" + exp + 어빌리티 목록).
/// AC-B B4: 공격 수치(쿨다운·사거리·데미지·CC)는 여기 없다 — abilities.json 의 어빌리티가 갖는다.
/// SocketServer Server.Monster.MonsterCatalog 는 이 위에서 시뮬 스탯(+어빌리티 해석)을 매핑하는 어댑터.
/// </summary>
public class MonsterCatalogTests
{
    [Fact]
    public void 임베디드_creepy_demon_정의가_로드된다()
    {
        var demon = MonsterCatalog.Get("creepy_demon");

        Assert.Equal(40, demon.MaxHp);
        Assert.Equal(18, demon.ExpReward);                        // Main 킬 보상
        Assert.Contains("creepy_demon_attack", demon.AbilityIds); // 공격은 어빌리티로 저작
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
        Assert.Empty(MonsterCatalog.Get("unknown_monster").AbilityIds); // 어빌리티 없음 = 공격 안 함
    }

    [Fact]
    public void SocketServer_어댑터는_시뮬_스탯을_매핑하고_사거리는_어빌리티에서_파생한다()
    {
        var stats = global::Server.Monster.MonsterCatalog.Get("creepy_demon");
        var def = MonsterCatalog.Get("creepy_demon");

        Assert.Equal(def.MaxHp, stats.MaxHp);
        Assert.Equal(def.MoveSpeed, stats.MoveSpeed);
        Assert.Equal(def.AggroRange, stats.AggroRange);

        // AttackRange = 이 몬스터 어빌리티들의 최대 ActivationRange(파생) — MonsterDef 에는 더 이상 없다.
        var abilities = global::Server.Monster.MonsterCatalog.GetAbilities("creepy_demon");
        Assert.NotEmpty(abilities);
        Assert.Equal(abilities.Max(a => a.ActivationRange), stats.AttackRange);
    }

    [Fact]
    public void 어댑터가_몬스터_어빌리티를_저작순서로_해석한다()
    {
        var abilities = global::Server.Monster.MonsterCatalog.GetAbilities("arachnya");

        var attack = Assert.Single(abilities);
        Assert.Equal("arachnya_attack", attack.Id);
        Assert.Equal(14, attack.BaseDamage);                          // 이관 값(밸런스 무변경)
        Assert.Equal(1600, attack.Timeline.CooldownMs);
        Assert.Contains("slow_3s", attack.Timeline.OnHitEffectIds);   // CC 는 어빌리티가 소유
    }

    [Fact]
    public void 미등록_몬스터는_어빌리티가_없어_공격_사거리가_0이다()
    {
        var stats = global::Server.Monster.MonsterCatalog.Get("unknown_monster");

        Assert.Empty(global::Server.Monster.MonsterCatalog.GetAbilities("unknown_monster"));
        Assert.Equal(0f, stats.AttackRange); // 접근만 하고 공격은 안 함
    }

    [Fact]
    public void 합성_JSON을_파싱한다()
    {
        const string json = """
        {
          "monsters": [
            { "monsterId": "goblin", "maxHp": 50, "moveSpeed": 3.0, "aggroRange": 8, "abilityIds": ["goblin_attack"], "expReward": 35 }
          ]
        }
        """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var table = MonsterCatalog.Parse(stream);

        Assert.True(table.ContainsKey("goblin"));
        Assert.Equal(50, table["goblin"].MaxHp);
        Assert.Equal(35, table["goblin"].ExpReward);
        Assert.Contains("goblin_attack", table["goblin"].AbilityIds);
    }
}
