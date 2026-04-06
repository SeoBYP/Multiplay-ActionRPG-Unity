using GameServer.Infrastructure.Domains;
using GameServer.Infrastructure.Domains.User;
using Microsoft.Extensions.Logging.Abstractions;

namespace GameServer.Tests.Infrastructure.Integrations;

[Collection("RepositoryIntegrationTests")]
public class UserRepositoryIntegrationTests
{
    private readonly RepositoryTestFixture _fixture;

    public UserRepositoryIntegrationTests(RepositoryTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Create_ShouldInsertIntoDbAndSetCache()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var repository = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var db = _fixture.RedisConnection.GetDatabase();

        // Act
        var user = await repository.CreateAsync();

        // Assert
        // 1. DB Insert 확인
        var dbUser = await context.Users.FindAsync(user.UserId);
        Assert.NotNull(dbUser);
        Assert.Equal(user.PublicId, dbUser.PublicId);

        // 2. Redis Hash 캐시 확인
        var cacheKey = RedisKeys.User(user.UserId);
        var hashEntries = await db.HashGetAllAsync(cacheKey);
        Assert.NotEmpty(hashEntries);
        var entriesDict = hashEntries.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());
        Assert.Equal(user.UserId.ToString(), entriesDict["UserId"]);
        Assert.Equal(user.PublicId, entriesDict["PublicId"]);

        // 3. Mapping 확인
        var mappingKey = RedisKeys.UserPublicIdMapping(user.PublicId);
        var mappingValue = await db.StringGetAsync(mappingKey);
        Assert.Equal(user.UserId.ToString(), mappingValue.ToString());
    }

    [Fact]
    public async Task Read_Hit_ShouldReturnFromCacheWithoutDbAccess()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var repository = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var db = _fixture.RedisConnection.GetDatabase();

        var user = await repository.CreateAsync();
        var cacheKey = RedisKeys.User(user.UserId);
        
        // DB 데이터를 지워서 캐시에서 가져오는지 확인 (Hit 검증)
        context.Users.Remove(user);
        await context.SaveChangesAsync();

        // Act
        var foundUser = await repository.GetByIdAsync(user.UserId);

        // Assert
        Assert.NotNull(foundUser);
        Assert.Equal(user.PublicId, foundUser.PublicId);
    }

    [Fact]
    public async Task Read_Miss_ShouldReturnFromDbAndSetCache()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var repository = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var db = _fixture.RedisConnection.GetDatabase();

        var user = await repository.CreateAsync();
        var cacheKey = RedisKeys.User(user.UserId);
        var mappingKey = RedisKeys.UserPublicIdMapping(user.PublicId);

        // Redis 캐시 삭제 (Miss 유도)
        await db.KeyDeleteAsync(cacheKey);
        await db.KeyDeleteAsync(mappingKey);

        // Act
        var foundUser = await repository.GetByIdAsync(user.UserId);

        // Assert
        Assert.NotNull(foundUser);
        Assert.Equal(user.UserId, foundUser.UserId);

        // 캐시 재설정 확인
        Assert.True(await db.KeyExistsAsync(cacheKey));
        Assert.True(await db.KeyExistsAsync(mappingKey));
    }

    [Fact]
    public async Task Update_ShouldUpdateDbAndClearCache()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var repository = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var db = _fixture.RedisConnection.GetDatabase();

        var user = await repository.CreateAsync();
        var oldPublicId = user.PublicId;
        var newPublicId = Guid.NewGuid().ToString()[..10]; // MaxPublicIdLength(10) 준수

        // User 객체의 PublicId는 private set일 수 있으므로 리플렉션이나 다른 생성 방식 확인 필요
        // 엔티티 구조상 PublicId 변경이 가능한지 확인 (일반적으로 PublicId는 고정이지만 테스트 시나리오상 시도)
        // UserRepository.UpdateAsync 내부에서 User.PublicId를 참조함.
        
        // 강제로 DB에서 변경
        var dbUser = await context.Users.FindAsync(user.UserId);
        dbUser!.GetType().GetProperty("PublicId")?.SetValue(dbUser, newPublicId);
        
        // Act
        await repository.UpdateAsync(dbUser);

        // Assert
        // 1. DB 확인
        var updatedDbUser = await context.Users.FindAsync(user.UserId);
        Assert.Equal(newPublicId, updatedDbUser?.PublicId);

        // 2. Redis 캐시 삭제 확인
        Assert.False(await db.KeyExistsAsync(RedisKeys.User(user.UserId)));
        Assert.False(await db.KeyExistsAsync(RedisKeys.UserPublicIdMapping(oldPublicId)));
        Assert.False(await db.KeyExistsAsync(RedisKeys.UserPublicIdMapping(newPublicId)));
    }

    [Fact]
    public async Task Delete_ShouldRemoveFromDbAndClearCache()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var repository = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var db = _fixture.RedisConnection.GetDatabase();

        var user = await repository.CreateAsync();
        var cacheKey = RedisKeys.User(user.UserId);
        var mappingKey = RedisKeys.UserPublicIdMapping(user.PublicId);

        // Act
        await repository.RemoveAsync(user.UserId);

        // Assert
        // 1. DB 삭제 확인
        var dbUser = await context.Users.FindAsync(user.UserId);
        Assert.Null(dbUser);

        // 2. Redis 캐시 삭제 확인
        Assert.False(await db.KeyExistsAsync(cacheKey));
        Assert.False(await db.KeyExistsAsync(mappingKey));
    }

    [Fact]
    public async Task TTL_ShouldBeSetOnCacheKeys()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var repository = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var db = _fixture.RedisConnection.GetDatabase();

        // Act
        var user = await repository.CreateAsync();
        var cacheKey = RedisKeys.User(user.UserId);
        var mappingKey = RedisKeys.UserPublicIdMapping(user.PublicId);

        // Assert
        var ttlUser = await db.KeyTimeToLiveAsync(cacheKey);
        var ttlMapping = await db.KeyTimeToLiveAsync(mappingKey);

        Assert.NotNull(ttlUser);
        Assert.NotNull(ttlMapping);
        Assert.True(ttlUser.Value.TotalSeconds > 0);
        Assert.True(ttlUser.Value.TotalMinutes <= 30);
        Assert.True(ttlMapping.Value.TotalSeconds > 0);
        Assert.True(ttlMapping.Value.TotalMinutes <= 30);
    }
}
