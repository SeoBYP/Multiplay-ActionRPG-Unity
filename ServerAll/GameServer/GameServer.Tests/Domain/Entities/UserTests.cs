using GameServer.Domain;
using GameServer.Domain.Entities;
using GameServer.Domain.Entities.User;

namespace GameServer.Tests.Domain.Entities;

public class UserTests
{
    [Fact]
    public void Create메서드는_유효한_데이터를_생성한다()
    {
        // given
        var nickname = "testuser"; ;  // ← 빈 문자열
        var passwordHash = "hashed_password";
        var email = "test@example.com";
        
        // when
        var user = User.Create(nickname, passwordHash, email);
        
        // then
        Assert.NotNull(user);
        Assert.Equal(nickname, user.NickName);
        Assert.Equal(email, user.Email);
    }
    
    [Theory]
    [InlineData("안녕하세요")]
    [InlineData("한글닉네임")]
    [InlineData("한글_닉네임")]
    public void Create메서드는_한글_Nickname을_허용한다(string nickname)
    {
        // given
        var passwordHash = "hashed_password";
        var email = "test@example.com";
        
        // when
        var user = User.Create(nickname, passwordHash, email);
        
        // then
        Assert.NotNull(user);
        Assert.Equal(nickname, user.NickName);
        Assert.Equal(email, user.Email);
    }

    [Fact]
    public void Create_메서드는_nickname이_빈문자열이면_예외를_던진다()
    {
        // given
        var nickname = "";  // ← 빈 문자열
        var passwordHash = "hashed_password";
        var email = "test@example.com";
        
        Assert.Throws<ArgumentException>(() => User.Create(nickname, passwordHash, email));
    }
    
    [Fact]
    public void Create_메서드는_nickname이_null이면_예외를_던진다()
    {
        // given
        string nickname = null;  // ← null 주의!
        var password = "hashed_password";
        var email = "test@example.com";
    
        // when & then
        Assert.Throws<ArgumentException>(() => User.Create(nickname, password, email));
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("a")]
    public void Create_메서드는_nickname이_3자_미만이면_예외를_던진다(string nickname)
    {
        // given
        var password = "hashed_password";
        var email = "test@example.com";
        
        // when & then
        Assert.Throws<ArgumentException>(() => User.Create(nickname, password, email));
    }

    [Fact]
    public void Create_메서드는_nickname이_20자_초과면_예외를_던진다()
    {
        // given
        var nickname = new string('a', 21);
        var password = "hashed_password";
        var email = "test@example.com";
        
        // when & then
        Assert.Throws<ArgumentException>(() => User.Create(nickname, password, email));
    }
    
    [Fact]
    public void Create_메서드는_Email이_빈문자열이면_예외를_던진다()
    {
        // given
        var nickname = "testuser";
        var password = "hashed_password";
        var email = "";  // ← 빈 문자열
    
        // when & then
        Assert.Throws<ArgumentException>(() => User.Create(nickname, password, email));
    }

    [Theory]
    [InlineData("notanemail")] // @ 없음
    [InlineData("@test.com")] // 로컬 파트 없음
    [InlineData("test@")] // 도메인 없음
    [InlineData("test test@test.com")] // 공백
    public void Create_메서드는_Email_형식이_잘못되면_예외를_던진다(string email)
    {
        // given
        var nickname = "testuser";
        var password = "hashed_password";
        
        // when & then
        Assert.Throws<ArgumentException>(() => User.Create(nickname, password, email));
    }
    
    [Theory]
    [InlineData("user@name")]    // @ 포함
    [InlineData("user name")]    // 공백 포함
    [InlineData("user!123")]     // ! 포함
    [InlineData("user#test")]    // # 포함
    public void Create_메서드는_nickname에_특수문자가_있으면_예외를_던진다(string nickname)
    {
        // given
        var password = "hashed_password";
        var email = "test@example.com";
    
        // when & then
        Assert.Throws<ArgumentException>(() => User.Create(nickname, password, email));
    }

    [Fact]
    public void Create_메서드는_PublicId에_10자리의_ID값이_생성된다()
    {
        // given
        var nickname = "testuser";
        var passwordHash = "hashed_password";
        var email = "test@example.com";
        
        // when
        var user = User.Create(nickname, passwordHash, email);
        
        // then
        Assert.NotNull(user);
        Assert.Equal(nickname, user.NickName);
        Assert.Equal(email, user.Email);
        Assert.Equal(10, user.PublicId.Length);
    }
    
    [Fact]
    public void Create_메서드는_PublicId가_중복되지_않는다()
    {
        // given
        var nickname = "testuser";
        var passwordHash = "hashed_password";
        var email = "test@example.com";
        
        // when & then
        var ids = Enumerable.Range(0, 1000)
            .Select(_ =>  User.Create(nickname, passwordHash, email).PublicId)
            .ToHashSet();
        Assert.Equal(1000, ids.Count);
    }

    [Fact]
    public void Create_메서드의_PublicId는_null이나_빈값이_아니다()
    {
        // given
        var nickname = "testuser";
        var passwordHash = "hashed_password";
        var email = "test@example.com";
        
        // when
        var user1 = User.Create(nickname, passwordHash, email);

        // then
        Assert.NotNull(user1);
        Assert.NotNull(user1.PublicId);
        Assert.NotEmpty(user1.PublicId);
    }
    
    [Fact]
    public void Create_메서드의_PublicId는_허용된_문자만_포함한다()
    {
        // given
        var nickname = "testuser";
        var passwordHash = "hashed_password";
        var email = "test@example.com";
        
        // when
        var user1 = User.Create(nickname, passwordHash, email);

        // then
        Assert.NotNull(user1);
        Assert.True(user1.PublicId.All(c => Const.AllowedPublicIdChars.Contains(c)));
    }
    
}