using GameServer.Infrastructure.Domains;
using GameServer.Infrastructure.Domains.User;
using Microsoft.Extensions.Logging.Abstractions;

namespace GameServer.Tests.Infrastructure.Integrations;

[Collection("RepositoryIntegrationTests")]
public class UserCredentialRepositoryIntegrationTests
{
    private readonly RepositoryTestFixture _fixture;

    public UserCredentialRepositoryIntegrationTests(RepositoryTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Create_ShouldInsertIntoDbAndSetCache()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var repository = new UserCredentialRepository(_fixture.RedisConnection, context, NullLogger<UserCredentialRepository>.Instance);
        var db = _fixture.RedisConnection.GetDatabase();

        long userId = 1001;
        string email = "test1@example.com";
        string passwordHash = "hash123";

        // Act
        var credential = await repository.CreateAsync(userId, email, passwordHash);

        // Assert
        // 1. DB Insert 확인
        var dbCredential = await context.UserCredentials.FindAsync(userId);
        Assert.NotNull(dbCredential);
        Assert.Equal(email, dbCredential.Email);

        // 2. Redis Hash 캐시 확인
        var cacheKey = RedisKeys.UserCredential(userId);
        var hashEntries = await db.HashGetAllAsync(cacheKey);
        Assert.NotEmpty(hashEntries);
        var entriesDict = hashEntries.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());
        Assert.Equal(userId.ToString(), entriesDict["UserId"]);
        Assert.Equal(email, entriesDict["Email"]);

        // 3. Mapping 확인
        var mappingKey = RedisKeys.UserCredentialEmailMapping(email);
        var mappingValue = await db.StringGetAsync(mappingKey);
        Assert.Equal(userId.ToString(), mappingValue.ToString());
    }

    [Fact]
    public async Task Read_Hit_ShouldReturnFromCacheWithoutDbAccess()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var repository = new UserCredentialRepository(_fixture.RedisConnection, context, NullLogger<UserCredentialRepository>.Instance);
        
        var credential = await repository.CreateAsync(1002, "test2@example.com", "hash");
        
        // DB 데이터를 지워서 캐시에서 가져오는지 확인
        context.UserCredentials.Remove(credential);
        await context.SaveChangesAsync();

        // Act
        var found = await repository.FindByIdAsync(1002);

        // Assert
        Assert.NotNull(found);
        Assert.Equal("test2@example.com", found.Email);
    }

    [Fact]
    public async Task Read_Miss_ShouldReturnFromDbAndSetCache()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var repository = new UserCredentialRepository(_fixture.RedisConnection, context, NullLogger<UserCredentialRepository>.Instance);
        var db = _fixture.RedisConnection.GetDatabase();

        var credential = await repository.CreateAsync(1003, "test3@example.com", "hash");
        var cacheKey = RedisKeys.UserCredential(1003);
        var mappingKey = RedisKeys.UserCredentialEmailMapping("test3@example.com");

        // Redis 캐시 삭제
        await db.KeyDeleteAsync(cacheKey);
        await db.KeyDeleteAsync(mappingKey);

        // Act
        var found = await repository.FindByIdAsync(1003);

        // Assert
        Assert.NotNull(found);
        Assert.Equal("test3@example.com", found.Email);

        // 캐시 재설정 확인
        Assert.True(await db.KeyExistsAsync(cacheKey));
        Assert.True(await db.KeyExistsAsync(mappingKey));
    }

    [Fact]
    public async Task Update_ShouldUpdateDbAndClearCache()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var repository = new UserCredentialRepository(_fixture.RedisConnection, context, NullLogger<UserCredentialRepository>.Instance);
        var db = _fixture.RedisConnection.GetDatabase();

        var credential = await repository.CreateAsync(1004, "old@example.com", "oldHash");
        credential.UpdatePasswordHash("newHash");

        // Act
        await repository.UpdateAsync(credential);

        // Assert
        // 1. DB 확인
        var dbCredential = await context.UserCredentials.FindAsync((long)1004);
        Assert.Equal("newHash", dbCredential?.PasswordHash);

        // 2. Redis 캐시 삭제 확인
        Assert.False(await db.KeyExistsAsync(RedisKeys.UserCredential(1004)));
        Assert.False(await db.KeyExistsAsync(RedisKeys.UserCredentialEmailMapping("old@example.com")));
    }

    [Fact]
    public async Task Delete_ShouldRemoveFromDbAndClearCache()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var repository = new UserCredentialRepository(_fixture.RedisConnection, context, NullLogger<UserCredentialRepository>.Instance);
        var db = _fixture.RedisConnection.GetDatabase();

        await repository.CreateAsync(1005, "delete@example.com", "hash");

        // Act
        await repository.RemoveAsync(1005);

        // Assert
        var dbCredential = await context.UserCredentials.FindAsync((long)1005);
        Assert.Null(dbCredential);

        Assert.False(await db.KeyExistsAsync(RedisKeys.UserCredential(1005)));
        Assert.False(await db.KeyExistsAsync(RedisKeys.UserCredentialEmailMapping("delete@example.com")));
    }

    [Fact]
    public async Task TTL_ShouldBeSetOnCacheKeys()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var repository = new UserCredentialRepository(_fixture.RedisConnection, context, NullLogger<UserCredentialRepository>.Instance);
        var db = _fixture.RedisConnection.GetDatabase();

        await repository.CreateAsync(1006, "ttl@example.com", "hash");
        var cacheKey = RedisKeys.UserCredential(1006);
        var mappingKey = RedisKeys.UserCredentialEmailMapping("ttl@example.com");

        // Assert
        var ttlUser = await db.KeyTimeToLiveAsync(cacheKey);
        var ttlMapping = await db.KeyTimeToLiveAsync(mappingKey);

        Assert.NotNull(ttlUser);
        Assert.NotNull(ttlMapping);
        Assert.True(ttlUser.Value.TotalSeconds > 0);
        Assert.True(ttlUser.Value.TotalMinutes <= 30);
    }
}
