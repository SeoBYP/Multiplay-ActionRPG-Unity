using GameServer.Infrastructure.Domains;
using GameServer.Infrastructure.Domains.User;
using Microsoft.Extensions.Logging.Abstractions;

namespace GameServer.Tests.Infrastructure.Integrations;

[Collection("RepositoryIntegrationTests")]
public class UserProfileRepositoryIntegrationTests
{
    private readonly RepositoryTestFixture _fixture;

    public UserProfileRepositoryIntegrationTests(RepositoryTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Create_ShouldInsertIntoDbAndSetCache()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var userRepo = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var user = await userRepo.CreateAsync();

        var repository = new UserProfileRepository(_fixture.RedisConnection, context, NullLogger<UserProfileRepository>.Instance);
        var db = _fixture.RedisConnection.GetDatabase();

        long userId = user.UserId;
        string nickName = "Player1";

        // Act
        var profile = await repository.CreateAsync(userId, nickName);

        // Assert
        // 1. DB Insert 확인
        var dbProfile = await context.UserProfiles.FindAsync(userId);
        Assert.NotNull(dbProfile);
        Assert.Equal(nickName, dbProfile.NickName);

        // 2. Redis Hash 캐시 확인
        var cacheKey = RedisKeys.UserProfile(userId);
        var hashEntries = await db.HashGetAllAsync(cacheKey);
        Assert.NotEmpty(hashEntries);
        var entriesDict = hashEntries.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());
        Assert.Equal(userId.ToString(), entriesDict["UserId"]);
        Assert.Equal(nickName, entriesDict["NickName"]);
    }

    [Fact]
    public async Task Read_Hit_ShouldReturnFromCacheWithoutDbAccess()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var userRepo = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var user = await userRepo.CreateAsync();

        var repository = new UserProfileRepository(_fixture.RedisConnection, context, NullLogger<UserProfileRepository>.Instance);
        
        var profile = await repository.CreateAsync(user.UserId, "Player2");
        
        // DB 데이터를 지워서 캐시에서 가져오는지 확인
        context.UserProfiles.Remove(profile);
        await context.SaveChangesAsync();

        // Act
        var found = await repository.GetByIdAsync(user.UserId);

        // Assert
        Assert.NotNull(found);
        Assert.Equal("Player2", found.NickName);
    }

    [Fact]
    public async Task Read_Miss_ShouldReturnFromDbAndSetCache()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var userRepo = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var user = await userRepo.CreateAsync();

        var repository = new UserProfileRepository(_fixture.RedisConnection, context, NullLogger<UserProfileRepository>.Instance);
        var db = _fixture.RedisConnection.GetDatabase();

        await repository.CreateAsync(user.UserId, "Player3");
        var cacheKey = RedisKeys.UserProfile(user.UserId);

        // Redis 캐시 삭제
        await db.KeyDeleteAsync(cacheKey);

        // Act
        var found = await repository.GetByIdAsync(user.UserId);

        // Assert
        Assert.NotNull(found);
        Assert.Equal("Player3", found.NickName);

        // 캐시 재설정 확인
        Assert.True(await db.KeyExistsAsync(cacheKey));
    }

    [Fact]
    public async Task Update_ShouldUpdateDbAndClearCache()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var userRepo = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var user = await userRepo.CreateAsync();

        var repository = new UserProfileRepository(_fixture.RedisConnection, context, NullLogger<UserProfileRepository>.Instance);
        var db = _fixture.RedisConnection.GetDatabase();

        var profile = await repository.CreateAsync(user.UserId, "OldNick");
        profile.SetNickName("NewNick");

        // Act
        await repository.UpdateAsync(profile);

        // Assert
        // 1. DB 확인 (새 context 사용)
        using var assertContext = _fixture.CreateDbContext();
        var dbProfile = await assertContext.UserProfiles.FindAsync(user.UserId);
        Assert.Equal("NewNick", dbProfile?.NickName);

        // 2. Redis 캐시 삭제 확인
        Assert.False(await db.KeyExistsAsync(RedisKeys.UserProfile(user.UserId)));
    }

    [Fact]
    public async Task Delete_ShouldRemoveFromDbAndClearCache()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var userRepo = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var user = await userRepo.CreateAsync();

        var repository = new UserProfileRepository(_fixture.RedisConnection, context, NullLogger<UserProfileRepository>.Instance);
        var db = _fixture.RedisConnection.GetDatabase();

        await repository.CreateAsync(user.UserId, "DeleteMe");

        // Act
        await repository.RemoveAsync(user.UserId);

        // Assert
        var dbProfile = await context.UserProfiles.FindAsync(user.UserId);
        Assert.Null(dbProfile);

        Assert.False(await db.KeyExistsAsync(RedisKeys.UserProfile(user.UserId)));
    }

    [Fact]
    public async Task TTL_ShouldBeSetOnCacheKeys()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var userRepo = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var user = await userRepo.CreateAsync();

        var repository = new UserProfileRepository(_fixture.RedisConnection, context, NullLogger<UserProfileRepository>.Instance);
        var db = _fixture.RedisConnection.GetDatabase();

        await repository.CreateAsync(user.UserId, "TTLPlayer");
        var cacheKey = RedisKeys.UserProfile(user.UserId);

        // Assert
        var ttl = await db.KeyTimeToLiveAsync(cacheKey);

        Assert.NotNull(ttl);
        Assert.True(ttl.Value.TotalSeconds > 0);
        Assert.True(ttl.Value.TotalMinutes <= 30);
    }
}
