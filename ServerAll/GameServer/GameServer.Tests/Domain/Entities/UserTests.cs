using GameServer.Domain.Entities;
using GameServer.Domain.Entities.User;

namespace GameServer.Tests.Domain.Entities;

public class UserTests
{
    [Fact]
    public void Create메서드는_유효한_데이터를_생성한다()
    {
        // given
        var username = "testuser"; ;  // ← 빈 문자열
        var passwordHash = "hashed_password";
        var email = "test@example.com";
        
        // when
        var user = User.Create(username, passwordHash, email);
        
        // then
        Assert.NotNull(user);
        Assert.Equal(username, user.UserName);
        Assert.Equal(email, user.Email);
    }

    [Fact]
    public void Create_메서드는_Username이_빈문자열이면_예외를_던진다()
    {
        // given
        var username = "";  // ← 빈 문자열
        var passwordHash = "hashed_password";
        var email = "test@example.com";
        
        Assert.Throws<ArgumentException>(() => User.Create(username, passwordHash, email));
    }
    
    [Fact]
    public void Create_메서드는_Username이_null이면_예외를_던진다()
    {
        // given
        string username = null;  // ← null 주의!
        var password = "hashed_password";
        var email = "test@example.com";
    
        // when & then
        Assert.Throws<ArgumentException>(() => User.Create(username, password, email));
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("a")]
    public void Create_메서드는_Username이_3자_미만이면_예외를_던진다(string username)
    {
        // given
        var password = "hashed_password";
        var email = "test@example.com";
        
        // when & then
        Assert.Throws<ArgumentException>(() => User.Create(username, password, email));
    }

    [Fact]
    public void Create_메서드는_Username이_20자_초과면_예외를_던진다()
    {
        // given
        var username = new string('a', 21);
        var password = "hashed_password";
        var email = "test@example.com";
        
        // when & then
        Assert.Throws<ArgumentException>(() => User.Create(username, password, email));
    }
    
    [Fact]
    public void Create_메서드는_Email이_빈문자열이면_예외를_던진다()
    {
        // given
        var username = "testuser";
        var password = "hashed_password";
        var email = "";  // ← 빈 문자열
    
        // when & then
        Assert.Throws<ArgumentException>(() => User.Create(username, password, email));
    }

    [Theory]
    [InlineData("notanemail")] // @ 없음
    [InlineData("@test.com")] // 로컬 파트 없음
    [InlineData("test@")] // 도메인 없음
    [InlineData("test test@test.com")] // 공백
    public void Create_메서드는_Email_형식이_잘못되면_예외를_던진다(string email)
    {
        // given
        var username = "testuser";
        var password = "hashed_password";
        
        // when & then
        Assert.Throws<ArgumentException>(() => User.Create(username, password, email));
    }
    
    [Theory]
    [InlineData("user@name")]    // @ 포함
    [InlineData("user name")]    // 공백 포함
    [InlineData("user!123")]     // ! 포함
    [InlineData("user#test")]    // # 포함
    public void Create_메서드는_Username에_특수문자가_있으면_예외를_던진다(string username)
    {
        // given
        var password = "hashed_password";
        var email = "test@example.com";
    
        // when & then
        Assert.Throws<ArgumentException>(() => User.Create(username, password, email));
    }
}