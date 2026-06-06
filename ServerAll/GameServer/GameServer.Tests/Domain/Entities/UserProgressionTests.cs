using GameServer.Domain.Entities.User;

namespace GameServer.Tests.Domain.Entities;

public class UserProgressionTests
{
    [Fact]
    public void Create_하면_레벨1_경험치0_으로_시작한다()
    {
        var progression = UserProgression.Create(userId: 1L);

        Assert.Equal(1L, progression.UserId);
        Assert.Equal(UserProgression.InitialLevel, progression.Level);
        Assert.Equal(0L, progression.Exp);
        Assert.True(progression.UpdatedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void Create_는_userId가_0이하면_예외를_던진다()
    {
        Assert.Throws<ArgumentException>(() => UserProgression.Create(0L));
        Assert.Throws<ArgumentException>(() => UserProgression.Create(-1L));
    }

    [Fact]
    public void 경험치를_적립하면_누적된다()
    {
        var progression = UserProgression.Create(1L);

        progression.AddExp(50);
        progression.AddExp(30);

        Assert.Equal(80L, progression.Exp);
    }

    [Fact]
    public void 적립한_경험치가_0이하이면_무시된다()
    {
        var progression = UserProgression.Create(1L);
        progression.AddExp(100);

        progression.AddExp(0);
        progression.AddExp(-10);

        Assert.Equal(100L, progression.Exp);
    }

    [Fact]
    public void 경험치_적립은_UpdatedAt을_갱신한다()
    {
        var progression = UserProgression.FromRedis(1L, level: 1, exp: 0, updatedAt: DateTime.UnixEpoch);
        var before = progression.UpdatedAt;

        progression.AddExp(10);

        Assert.True(progression.UpdatedAt > before);
    }
}
