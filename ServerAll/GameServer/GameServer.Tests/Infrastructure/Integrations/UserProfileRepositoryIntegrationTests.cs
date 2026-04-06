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
        var repository = new UserProfileRepository(_fixture.RedisConnection, context, NullLogger<UserProfileRepository>.Instance);
        var db = _fixture.RedisConnection.GetDatabase();

        long userId = 2001;
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
        var repository = new UserProfileRepository(_fixture.RedisConnection, context, NullLogger<UserProfileRepository>.Instance);
        
        var profile = await repository.CreateAsync(2002, "Player2");
        
        // DB 데이터를 지워서 캐시에서 가져오는지 확인
        context.UserProfiles.Remove(profile);
        await context.SaveChangesAsync();

        // Act
        var found = await repository.GetByIdAsync(2002);

        // Assert
        Assert.NotNull(found);
        Assert.Equal("Player2", found.NickName);
    }

    [Fact]
    public async Task Read_Miss_ShouldReturnFromDbAndSetCache()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var repository = new UserProfileRepository(_fixture.RedisConnection, context, NullLogger<UserProfileRepository>.Instance);
        var db = _fixture.RedisConnection.GetDatabase();

        await repository.CreateAsync(2003, "Player3");
        var cacheKey = RedisKeys.UserProfile(2003);

        // Redis 캐시 삭제
        await db.KeyDeleteAsync(cacheKey);

        // Act
        var found = await repository.GetByIdAsync(2003);

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
        var repository = new UserProfileRepository(_fixture.RedisConnection, context, NullLogger<UserProfileRepository>.Instance);
        var db = _fixture.RedisConnection.GetDatabase();

        var profile = await repository.CreateAsync(2004, "OldNick");
        profile.SetNickName("NewNick");

        // Act
        await repository.UpdateAsync(profile);

        // Assert
        // 1. DB 확인
        var dbProfile = await context.UserProfiles.FindAsync((long)2004);
        Assert.Equal("NewNick", dbProfile?.NickName);

        // 2. Redis 캐시 삭제 확인
        Assert.False(await db.KeyExistsAsync(RedisKeys.UserProfile(2004)));
    }

    [Fact]
    public async Task Delete_ShouldRemoveFromDbAndClearCache()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var repository = new UserProfileRepository(_fixture.RedisConnection, context, NullLogger<UserProfileRepository>.Instance);
        var db = _fixture.RedisConnection.GetDatabase();

        await repository.CreateAsync(2005, "DeleteMe");

        // Act
        await repository.RemoveAsync(2005);

        // Assert
        var dbProfile = await context.UserProfiles.FindAsync((long)2005);
        Assert.Null(dbProfile);

        Assert.False(await db.KeyExistsAsync(RedisKeys.UserProfile(2005)));
    }

    [Fact]
    public async Task TTL_ShouldBeSetOnCacheKeys()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var repository = new UserProfileRepository(_fixture.RedisConnection, context, NullLogger<UserProfileRepository>.Instance);
        var db = _fixture.RedisConnection.GetDatabase();

        await repository.CreateAsync(2006, "TTLPlayer");
        var cacheKey = RedisKeys.UserProfile(2006);

        // Assert
        var ttl = await db.KeyTimeToLiveAsync(cacheKey);

        Assert.NotNull(ttl);
        Assert.True(ttl.Value.TotalSeconds > 0);
        Assert.True(ttl.Value.TotalMinutes <= 30);
    }
}
