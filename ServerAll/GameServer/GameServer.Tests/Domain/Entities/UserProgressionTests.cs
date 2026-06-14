using GameServer.Domain.Entities.User;

namespace GameServer.Tests.Domain.Entities;

public class UserProgressionTests
{
    /// <summary>절대 레벨업하지 않는 곡선 — 누적/무시 의미만 검증하는 테스트용.</summary>
    private sealed class NoLevelUpCurve : ILevelCurve
    {
        public int MaxLevel => 60;
        public long ExpToNext(int level) => long.MaxValue;
    }

    /// <summary>모든 레벨 동일 임계 곡선 — 레벨업/remainder/만렙을 제어 가능하게 검증.</summary>
    private sealed class FlatCurve(int maxLevel, long perLevel) : ILevelCurve
    {
        public int MaxLevel => maxLevel;
        public long ExpToNext(int level) => perLevel;
    }

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
    public void 임계_미만이면_경험치가_누적되고_레벨은_그대로다()
    {
        var progression = UserProgression.Create(1L);
        var curve = new NoLevelUpCurve();

        progression.AddExp(50, curve);
        progression.AddExp(30, curve);

        Assert.Equal(80L, progression.Exp);
        Assert.Equal(1, progression.Level);
    }

    [Fact]
    public void 적립한_경험치가_0이하이면_무시된다()
    {
        var progression = UserProgression.Create(1L);
        var curve = new NoLevelUpCurve();
        progression.AddExp(100, curve);

        progression.AddExp(0, curve);
        progression.AddExp(-10, curve);

        Assert.Equal(100L, progression.Exp);
        Assert.Equal(1, progression.Level);
    }

    [Fact]
    public void 경험치_적립은_UpdatedAt을_갱신한다()
    {
        var progression = UserProgression.FromRedis(1L, level: 1, exp: 0, updatedAt: DateTime.UnixEpoch);
        var before = progression.UpdatedAt;

        progression.AddExp(10, new NoLevelUpCurve());

        Assert.True(progression.UpdatedAt > before);
    }

    [Fact]
    public void 임계를_정확히_채우면_레벨업하고_Exp는_0이_된다()
    {
        var progression = UserProgression.Create(1L);

        progression.AddExp(100, new FlatCurve(maxLevel: 60, perLevel: 100));

        Assert.Equal(2, progression.Level);
        Assert.Equal(0L, progression.Exp);
    }

    [Fact]
    public void 한_번의_적립으로_여러_레벨_점프하고_remainder가_이월된다()
    {
        var progression = UserProgression.Create(1L);

        // 250 = 100(→Lv2) + 100(→Lv3) + 50 잔여
        progression.AddExp(250, new FlatCurve(maxLevel: 60, perLevel: 100));

        Assert.Equal(3, progression.Level);
        Assert.Equal(50L, progression.Exp);
    }

    [Fact]
    public void 만렙에_도달하면_레벨업을_멈추고_남은_Exp는_잔류한다()
    {
        var progression = UserProgression.Create(1L);

        // maxLevel=2 → Lv1에서 1회만 레벨업, 나머지 400은 만렙 잔류
        progression.AddExp(500, new FlatCurve(maxLevel: 2, perLevel: 100));

        Assert.Equal(2, progression.Level);
        Assert.Equal(400L, progression.Exp);
    }
}
