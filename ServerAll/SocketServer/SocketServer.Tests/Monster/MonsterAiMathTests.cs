using Server.Monster;
using Shared.Infrastructure.Spawn;

namespace Server.Tests.Monster;

/// <summary>
/// M3 증분④: MonsterAiMath 순수 이동 수식. Chase/Attack/Patrol/Idle 분기 + 경계 clamp.
/// </summary>
public class MonsterAiMathTests
{
    private static readonly MonsterStats Stats =
        new(MaxHp: 30, MoveSpeed: 2f, AggroRange: 6f, AttackRange: 1.2f, AttackCooldownMs: 1500f, AttackDamage: 5);

    private static readonly MapBounds Bounds40 = new(0f, 0f, 40f, 40f); // x,z ∈ [-20, 20]

    private static MonsterState NewMonster(float x, float z, params PatrolPoint[] patrol) => new()
    {
        InstanceId = 1,
        MonsterId = "slime",
        PosX = x,
        PosZ = z,
        SpawnX = x,
        SpawnZ = z,
        MaxHp = 30,
        Hp = 30,
        Phase = MonsterPhase.Idle,
        Patrol = patrol,
    };

    [Fact]
    public void 추격_aggro범위_플레이어쪽으로_이동하고_Chase페이즈()
    {
        var m = NewMonster(0f, 0f);
        var players = new List<PlayerPos> { new(5f, 0f) }; // dist 5 ≤ aggro 6, > attack 1.2

        MonsterAiMath.Step(m, players, Bounds40, Stats, 0.5f); // step = 2*0.5 = 1

        Assert.Equal(MonsterPhase.Chase, m.Phase);
        Assert.Equal(1f, m.PosX, 3);
        Assert.Equal(0f, m.PosZ, 3);
    }

    [Fact]
    public void 공격_사거리_안이면_정지하고_Attack페이즈()
    {
        var m = NewMonster(0f, 0f);
        var players = new List<PlayerPos> { new(1f, 0f) }; // dist 1 ≤ attack 1.2

        MonsterAiMath.Step(m, players, Bounds40, Stats, 0.5f);

        Assert.Equal(MonsterPhase.Attack, m.Phase);
        Assert.Equal(0f, m.PosX, 3); // 정지
        Assert.True(MathF.Abs(m.RotY - 90f) < 0.01f); // +X 방향을 바라봄
    }

    [Fact]
    public void 패트롤_플레이어_없으면_웨이포인트로_이동하고_Patrol페이즈()
    {
        var m = NewMonster(0f, 0f, new PatrolPoint(4f, 0f));
        var players = new List<PlayerPos>(); // 플레이어 없음

        MonsterAiMath.Step(m, players, Bounds40, Stats, 0.5f); // step 1 → x=1

        Assert.Equal(MonsterPhase.Patrol, m.Phase);
        Assert.Equal(1f, m.PosX, 3);
    }

    [Fact]
    public void 패트롤_웨이포인트_도달시_다음_인덱스로_넘어간다()
    {
        var m = NewMonster(4f, 0f, new PatrolPoint(4f, 0f), new PatrolPoint(8f, 0f)); // 이미 wp0 위
        var players = new List<PlayerPos>();

        MonsterAiMath.Step(m, players, Bounds40, Stats, 0.5f);

        Assert.Equal(1, m.PatrolIndex); // wp0 도달 → 다음
    }

    [Fact]
    public void 경계를_벗어나는_이동은_clamp된다()
    {
        var m = NewMonster(19f, 0f, new PatrolPoint(100f, 0f)); // 경계 밖으로 향함
        var players = new List<PlayerPos>();
        var fastStats = Stats with { MoveSpeed = 10f };

        MonsterAiMath.Step(m, players, Bounds40, fastStats, 1f); // 19 + 10 = 29 → clamp 20

        Assert.Equal(20f, m.PosX, 3);
    }

    [Fact]
    public void 플레이어도_패트롤도_없으면_Idle_제자리()
    {
        var m = NewMonster(5f, 5f);
        var players = new List<PlayerPos>();

        MonsterAiMath.Step(m, players, Bounds40, Stats, 0.5f);

        Assert.Equal(MonsterPhase.Idle, m.Phase);
        Assert.Equal(5f, m.PosX, 3);
        Assert.Equal(5f, m.PosZ, 3);
    }

    [Fact]
    public void aggro_범위_밖_플레이어는_무시하고_패트롤한다()
    {
        var m = NewMonster(0f, 0f, new PatrolPoint(4f, 0f));
        var players = new List<PlayerPos> { new(100f, 0f) }; // dist 100 > aggro 6

        MonsterAiMath.Step(m, players, Bounds40, Stats, 0.5f);

        Assert.Equal(MonsterPhase.Patrol, m.Phase);
        Assert.Equal(1f, m.PosX, 3);
    }
}
