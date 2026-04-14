using GameServer.Application.Common;
using GameServer.Application.Domains.Account;
using GameServer.Application.Security.Interface;
using GameServer.Infrastructure.Security;
using GameServer.Tests.Infrastructure.Fakes.Repositories;
using Microsoft.Extensions.Logging.Abstractions;

namespace GameServer.Tests.Application.Services;

public class AccountServiceTests
{
    private readonly FakeUserRepository _userRepository = new();
    private readonly FakeUserProfileRepository _profileRepository = new();
    private readonly FakeUserCredentialRepository _credentialRepository = new();
    private readonly IPasswordHasher _passwordHasher = new PasswordHasher();

    private AccountService CreateService()
    {
        return new AccountService(
            _userRepository,
            _profileRepository,
            _credentialRepository,
            _passwordHasher,
            NullLogger<AccountService>.Instance);
    }

    [Fact]
    public async Task 회원가입_시_User와_UserCredential이_함께_생성된다()
    {
        var service = CreateService();

        var result = await service.RegisterAsync("test@example.com", "Password123!");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);

        var credential = await _credentialRepository.FindByIdAsync(result.Value!.UserId);
        Assert.NotNull(credential);
        Assert.Equal("test@example.com", credential!.Email);
        Assert.NotEqual("Password123!", credential.PasswordHash);
    }

    [Fact]
    public async Task 중복_이메일로_회원가입하면_실패한다()
    {
        var service = CreateService();
        await service.RegisterAsync("dup@example.com", "Password123!");

        var result = await service.RegisterAsync("dup@example.com", "Password123!");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.EmailAlreadyTaken, result.InternalErrorCode);
    }

    [Fact]
    public async Task 올바른_이메일과_비밀번호로_자격검증하면_성공한다()
    {
        var service = CreateService();
        var register = await service.RegisterAsync("verify@example.com", "Password123!");

        var result = await service.VerifyCredentialAsync("verify@example.com", "Password123!");

        Assert.True(result.IsSuccess);
        Assert.Equal(register.Value!.UserId, result.Value!.UserId);
        Assert.Equal(register.Value.PublicId, result.Value.PublicId);
    }

    [Fact]
    public async Task 잘못된_비밀번호로_자격검증하면_실패한다()
    {
        var service = CreateService();
        await service.RegisterAsync("verify-fail@example.com", "Password123!");

        var result = await service.VerifyCredentialAsync("verify-fail@example.com", "WrongPassword!");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.InvalidCredentials, result.InternalErrorCode);
    }

    [Fact]
    public async Task 비밀번호_변경_성공_후_새_비밀번호로만_검증된다()
    {
        var service = CreateService();
        var register = await service.RegisterAsync("password@example.com", "Password123!");

        var update = await service.UpdatePasswordAsync(register.Value!.UserId, "Password123!", "NewPassword123!");

        Assert.True(update.IsSuccess);
        Assert.False((await service.VerifyCredentialAsync("password@example.com", "Password123!")).IsSuccess);
        Assert.True((await service.VerifyCredentialAsync("password@example.com", "NewPassword123!")).IsSuccess);
    }
}
