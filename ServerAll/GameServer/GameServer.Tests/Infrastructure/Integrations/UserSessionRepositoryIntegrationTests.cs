using GameServer.Application.Security;
using GameServer.Infrastructure.Domains;
using Microsoft.EntityFrameworkCore;
using GameServer.Infrastructure.Domains.User;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GameServer.Tests.Infrastructure.Integrations;

[Collection("RepositoryIntegrationTests")]
public class UserSessionRepositoryIntegrationTests
{
    private readonly RepositoryTestFixture _fixture;
    private readonly IOptions<JwtOptions> _jwtOptions;

    public UserSessionRepositoryIntegrationTests(RepositoryTestFixture fixture)
    {
        _fixture = fixture;
        var options = new JwtOptions { AccessTokenMinutes = 30 };
        _jwtOptions = Options.Create(options);
    }

    [Fact]
    public async Task Create_ShouldInsertIntoDbAndSetCache()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var userRepo = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var user = await userRepo.CreateAsync();

        var repository = new UserSessionRepository(_fixture.RedisConnection, context, _jwtOptions, NullLogger<UserSessionRepository>.Instance);
        var db = _fixture.RedisConnection.GetDatabase();

        long userId = user.UserId;

        // Act
        var session = await repository.CreateSessionAsync(userId);

        // Assert
        Assert.NotNull(session);
        
        // 1. DB Insert 확인
        var dbSession = await context.UserSessions.FindAsync(session.SessionId);
        Assert.NotNull(dbSession);
        Assert.Equal(userId, dbSession.UserId);

        // 2. Redis Hash 캐시 확인
        var cacheKey = RedisKeys.UserSession(session.SessionId);
        var hashEntries = await db.HashGetAllAsync(cacheKey);
        Assert.NotEmpty(hashEntries);
        var entriesDict = hashEntries.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());
        Assert.Equal(userId.ToString(), entriesDict["UserId"]);

        // 3. Mapping 확인
        var mappingKey = RedisKeys.UserSessionMapping(userId);
        var mappingValue = await db.StringGetAsync(mappingKey);
        Assert.Equal(session.SessionId, mappingValue.ToString());
    }

    [Fact]
    public async Task Read_Hit_ShouldReturnFromCacheWithoutDbAccess()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var userRepo = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var user = await userRepo.CreateAsync();

        var repository = new UserSessionRepository(_fixture.RedisConnection, context, _jwtOptions, NullLogger<UserSessionRepository>.Instance);
        
        var session = await repository.CreateSessionAsync(user.UserId);
        
        // DB 데이터를 지워서 캐시에서 가져오는지 확인
        context.UserSessions.Remove(session!);
        await context.SaveChangesAsync();

        // Act
        var found = await repository.GetBySessionIdAsync(session!.SessionId);

        // Assert
        Assert.NotNull(found);
        Assert.Equal(user.UserId, found.UserId);
    }

    [Fact]
    public async Task Read_Miss_ShouldReturnFromDbAndSetCache()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var userRepo = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var user = await userRepo.CreateAsync();

        var repository = new UserSessionRepository(_fixture.RedisConnection, context, _jwtOptions, NullLogger<UserSessionRepository>.Instance);
        var db = _fixture.RedisConnection.GetDatabase();

        var session = await repository.CreateSessionAsync(user.UserId);
        var cacheKey = RedisKeys.UserSession(session!.SessionId);
        var mappingKey = RedisKeys.UserSessionMapping(user.UserId);

        // Redis 캐시 삭제
        await db.KeyDeleteAsync(cacheKey);
        await db.KeyDeleteAsync(mappingKey);

        // Act
        var found = await repository.GetBySessionIdAsync(session.SessionId);

        // Assert
        Assert.NotNull(found);
        Assert.Equal(user.UserId, found.UserId);

        // 캐시 재설정 확인
        Assert.True(await db.KeyExistsAsync(cacheKey));
        Assert.True(await db.KeyExistsAsync(mappingKey));
    }

    [Fact]
    public async Task Delete_ShouldRemoveFromDbAndClearCache()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var userRepo = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var user = await userRepo.CreateAsync();

        var repository = new UserSessionRepository(_fixture.RedisConnection, context, _jwtOptions, NullLogger<UserSessionRepository>.Instance);
        var db = _fixture.RedisConnection.GetDatabase();

        var session = await repository.CreateSessionAsync(user.UserId);

        // Act
        await repository.RemoveSessionAsync(session!.SessionId);

        // Assert
        var dbSession = await context.UserSessions.FindAsync(session.SessionId);
        Assert.Null(dbSession);

        Assert.False(await db.KeyExistsAsync(RedisKeys.UserSession(session.SessionId)));
        Assert.False(await db.KeyExistsAsync(RedisKeys.UserSessionMapping(user.UserId)));
    }

    [Fact]
    public async Task TTL_ShouldBeSetOnCacheKeys()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var userRepo = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var user = await userRepo.CreateAsync();

        var repository = new UserSessionRepository(_fixture.RedisConnection, context, _jwtOptions, NullLogger<UserSessionRepository>.Instance);
        var db = _fixture.RedisConnection.GetDatabase();

        var session = await repository.CreateSessionAsync(user.UserId);
        var cacheKey = RedisKeys.UserSession(session!.SessionId);

        // Assert
        var ttl = await db.KeyTimeToLiveAsync(cacheKey);

        Assert.NotNull(ttl);
        Assert.True(ttl.Value.TotalSeconds > 0);
        // JwtOptions에서 30분으로 설정함
        Assert.True(ttl.Value.TotalMinutes <= 30);
    }

    [Fact]
    public async Task 세션_활성_만료시각을_돌려주고_세션이_없으면_null이다()
    {
        // 리퍼의 유일한 생존 근사 신호. Redis 활성 집합의 score 를 그대로 읽어야 한다.
        using var context = _fixture.CreateDbContext();
        var userRepo = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var repository = new UserSessionRepository(_fixture.RedisConnection, context, _jwtOptions, NullLogger<UserSessionRepository>.Instance);
        var db = _fixture.RedisConnection.GetDatabase();

        var user = await userRepo.CreateAsync();
        var session = await repository.CreateSessionAsync(user.UserId);

        var activeUntil = await repository.GetSessionActiveUntilAsync(user.UserId);

        Assert.NotNull(activeUntil);
        var score = await db.SortedSetScoreAsync(RedisKeys.UserSessionActive(), session!.SessionId);
        Assert.NotNull(score);
        Assert.Equal(
            DateTimeOffset.FromUnixTimeSeconds((long)score!.Value).UtcDateTime,
            activeUntil!.Value);

        await repository.RemoveSessionAsync(session.SessionId);
        Assert.Null(await repository.GetSessionActiveUntilAsync(user.UserId));
    }


    [Fact]
    public async Task 세션을_touch_하면_활성_만료시각이_앞으로_밀린다()
    {
        using var context = _fixture.CreateDbContext();
        var userRepo = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var repository = new UserSessionRepository(_fixture.RedisConnection, context, _jwtOptions, NullLogger<UserSessionRepository>.Instance);
        var db = _fixture.RedisConnection.GetDatabase();

        var user = await userRepo.CreateAsync();
        var session = await repository.CreateSessionAsync(user.UserId);

        // 수명이 절반 이상 지난 상황을 만든다 — 스로틀이 이때만 쓰기를 허용한다.
        var stale = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds();
        await db.SortedSetAddAsync(RedisKeys.UserSessionActive(), session!.SessionId, stale);

        await repository.TouchSessionAsync(session.SessionId);

        var activeUntil = await repository.GetSessionActiveUntilAsync(user.UserId);
        Assert.NotNull(activeUntil);
        Assert.True(activeUntil!.Value > DateTimeOffset.FromUnixTimeSeconds(stale).UtcDateTime,
            "touch 는 활성 만료 시각을 앞으로 밀어야 한다");

        var dbSession = await context.UserSessions.AsNoTracking()
            .SingleAsync(us => us.SessionId == session.SessionId);
        Assert.True(dbSession.LastActiveAt > dbSession.LoginAt,
            "LastActiveAt 이 갱신되어야 한다(죽은 필드가 아니어야 한다)");
    }

    [Fact]
    public async Task Redis_활성기록이_사라져도_DB의_LastActiveAt으로_생존을_판단한다()
    {
        // 리퍼 오탐 방지: Redis 가 비워져도 살아 있는 세션을 "조용하다"고 오판하면 안 된다.
        using var context = _fixture.CreateDbContext();
        var userRepo = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var repository = new UserSessionRepository(_fixture.RedisConnection, context, _jwtOptions, NullLogger<UserSessionRepository>.Instance);
        var db = _fixture.RedisConnection.GetDatabase();

        var user = await userRepo.CreateAsync();
        var session = await repository.CreateSessionAsync(user.UserId);

        await db.SortedSetRemoveAsync(RedisKeys.UserSessionActive(), session!.SessionId);
        await db.KeyDeleteAsync(RedisKeys.UserSessionMapping(user.UserId));

        var activeUntil = await repository.GetSessionActiveUntilAsync(user.UserId);

        Assert.NotNull(activeUntil);
        Assert.True(activeUntil!.Value > DateTime.UtcNow,
            "DB LastActiveAt + AccessToken 수명으로 폴백해야 한다");
    }

}
