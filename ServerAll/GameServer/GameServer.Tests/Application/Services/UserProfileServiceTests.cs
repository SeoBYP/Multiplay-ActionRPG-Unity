using GameServer.Application.Common;
using GameServer.Application.Common.Interfaces;
using GameServer.Application.Domains.User;
using GameServer.Tests.Infrastructure.Fakes.Repositories;
using Microsoft.Extensions.Logging.Abstractions;

namespace GameServer.Tests.Application.Services;

public class UserProfileServiceTests
{
    private readonly FakeUserProfileRepository _profileRepository = new();
    private readonly FakeUserSessionRepository _sessionRepository = new();

    private UserProfileService CreateService(bool isProfane = false)
    {
        return new UserProfileService(
            _profileRepository,
            _sessionRepository,
            new StubProfanityFilter(isProfane),
            NullLogger<UserProfileService>.Instance);
    }

    [Fact]
    public async Task 유효한_세션으로_프로필_조회_시_성공한다()
    {
        await _profileRepository.CreateAsync(1, "PlayerOne");
        var session = await _sessionRepository.CreateSessionAsync(1);
        var service = CreateService();

        var result = await service.GetProfileAsync(session!.SessionId);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.UserId);
        Assert.Equal("PlayerOne", result.Value.NickName);
    }

    [Fact]
    public async Task 없는_세션으로_프로필_조회_시_실패한다()
    {
        var service = CreateService();

        var result = await service.GetProfileAsync("missing-session");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.InvalidRequest, result.InternalErrorCode);
    }

    [Fact]
    public async Task 유효한_닉네임으로_프로필_업데이트_시_성공한다()
    {
        await _profileRepository.CreateAsync(1, "Before");
        var session = await _sessionRepository.CreateSessionAsync(1);
        var service = CreateService();

        var result = await service.UpdateProfileAsync(session!.SessionId, "AfterName");

        Assert.True(result.IsSuccess);
        Assert.Equal("AfterName", result.Value!.NickName);
    }

    [Fact]
    public async Task 비속어가_포함된_닉네임은_거부된다()
    {
        await _profileRepository.CreateAsync(1, "Before");
        var session = await _sessionRepository.CreateSessionAsync(1);
        var service = CreateService(isProfane: true);

        var result = await service.UpdateProfileAsync(session!.SessionId, "BadWord");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.Profanity, result.InternalErrorCode);
    }

    [Fact]
    public async Task 형식이_잘못된_닉네임은_거부된다()
    {
        await _profileRepository.CreateAsync(1, "Before");
        var session = await _sessionRepository.CreateSessionAsync(1);
        var service = CreateService();

        var result = await service.UpdateProfileAsync(session!.SessionId, "a");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.InvalidRequest, result.InternalErrorCode);
    }

    private sealed class StubProfanityFilter(bool isProfane) : IProfanityFilter
    {
        public string Filter(string message) => message;

        public bool IsProfane(string message) => isProfane;
    }
}
