using GameServer.Domain;
using GameServer.Domain.Entities.User;

namespace GameServer.Tests.Domain.Entities;

public class UserTests
{
    [Fact]
    public void Create_메서드는_PublicId와_생성시각을_포함한_User를_생성한다()
    {
        var user = User.Create();

        Assert.NotNull(user);
        Assert.NotEmpty(user.PublicId);
        Assert.Equal(10, user.PublicId.Length);
        Assert.True(user.CreatedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void Create_메서드의_PublicId는_중복되지_않는다()
    {
        var ids = Enumerable.Range(0, 1000)
            .Select(_ => User.Create().PublicId)
            .ToHashSet();

        Assert.Equal(1000, ids.Count);
    }

    [Fact]
    public void Create_메서드의_PublicId는_허용된_문자만_포함한다()
    {
        var user = User.Create();

        Assert.True(user.PublicId.All(c => Const.AllowedPublicIdChars.Contains(c)));
    }

    [Fact]
    public void SetUserId는_한번만_설정할_수_있다()
    {
        var user = User.Create();
        user.SetUserId(1);

        Assert.Throws<InvalidOperationException>(() => user.SetUserId(2));
    }
}
