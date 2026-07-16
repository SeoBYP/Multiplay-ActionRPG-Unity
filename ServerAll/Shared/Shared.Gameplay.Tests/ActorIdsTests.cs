using Script.System.GamePlayAbilitySystem;

namespace Shared.Gameplay.Tests;

/// <summary>
/// ActorId 부호 규약 검증(actor-combat-architecture §2.1). 플레이어=양수 / 몬스터=음수 / 환경=0.
/// 이 규약이 깨지면 전투 통합 파이프의 라우팅·권위 소유가 전부 어긋나므로 왕복·판별을 못 박는다.
/// </summary>
public class ActorIdsTests
{
    [Fact]
    public void 플레이어_ActorId는_UserId_그대로_양수다()
    {
        Assert.Equal(42L, ActorIds.FromPlayer(42));
        Assert.True(ActorIds.IsPlayer(ActorIds.FromPlayer(42)));
    }

    [Fact]
    public void 몬스터_ActorId는_음수로_변환되고_InstanceId로_왕복된다()
    {
        long actorId = ActorIds.FromMonster(7);

        Assert.Equal(-7L, actorId);
        Assert.True(ActorIds.IsMonster(actorId));
        Assert.Equal(7, ActorIds.ToMonsterInstanceId(actorId));
    }

    [Fact]
    public void 환경_출처는_0이며_플레이어도_몬스터도_아니다()
    {
        Assert.Equal(0L, ActorIds.Environment);
        Assert.True(ActorIds.IsEnvironment(ActorIds.Environment));
        Assert.False(ActorIds.IsPlayer(ActorIds.Environment));
        Assert.False(ActorIds.IsMonster(ActorIds.Environment));
    }

    [Fact]
    public void 부호로_플레이어와_몬스터를_배타적으로_판별한다()
    {
        long player  = ActorIds.FromPlayer(1);
        long monster = ActorIds.FromMonster(1);

        Assert.True(ActorIds.IsPlayer(player));
        Assert.False(ActorIds.IsMonster(player));
        Assert.True(ActorIds.IsMonster(monster));
        Assert.False(ActorIds.IsPlayer(monster));
        // 같은 정수 1 이어도 플레이어(+1)와 몬스터(-1)는 절대 충돌하지 않는다.
        Assert.NotEqual(player, monster);
    }
}
