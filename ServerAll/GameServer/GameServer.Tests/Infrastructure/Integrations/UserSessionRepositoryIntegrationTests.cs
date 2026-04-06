using GameServer.Application.Security;
using GameServer.Infrastructure.Domains;
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
}
