using GameServer.Infrastructure.Domains;
using GameServer.Infrastructure.Domains.User;
using Microsoft.EntityFrameworkCore;
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
        var userRepo = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var user = await userRepo.CreateAsync();
        
        var repository = new UserCredentialRepository(_fixture.RedisConnection, context, NullLogger<UserCredentialRepository>.Instance);
        var db = _fixture.RedisConnection.GetDatabase();

        long userId = user.UserId;
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
        var userRepo = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var user = await userRepo.CreateAsync();

        var repository = new UserCredentialRepository(_fixture.RedisConnection, context, NullLogger<UserCredentialRepository>.Instance);
        
        var credential = await repository.CreateAsync(user.UserId, "test2@example.com", "hash");
        
        // DB 데이터를 지워서 캐시에서 가져오는지 확인
        context.UserCredentials.Remove(credential);
        await context.SaveChangesAsync();

        // Act
        var found = await repository.FindByIdAsync(user.UserId);

        // Assert
        Assert.NotNull(found);
        Assert.Equal("test2@example.com", found.Email);
    }

    [Fact]
    public async Task Read_Miss_ShouldReturnFromDbAndSetCache()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var userRepo = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var user = await userRepo.CreateAsync();

        var repository = new UserCredentialRepository(_fixture.RedisConnection, context, NullLogger<UserCredentialRepository>.Instance);
        var db = _fixture.RedisConnection.GetDatabase();

        var credential = await repository.CreateAsync(user.UserId, "test3@example.com", "hash");
        var cacheKey = RedisKeys.UserCredential(user.UserId);
        var mappingKey = RedisKeys.UserCredentialEmailMapping("test3@example.com");

        // Redis 캐시 삭제
        await db.KeyDeleteAsync(cacheKey);
        await db.KeyDeleteAsync(mappingKey);

        // Act
        var found = await repository.FindByIdAsync(user.UserId);

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
        var userRepo = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var user = await userRepo.CreateAsync();

        var repository = new UserCredentialRepository(_fixture.RedisConnection, context, NullLogger<UserCredentialRepository>.Instance);
        var db = _fixture.RedisConnection.GetDatabase();

        var credential = await repository.CreateAsync(user.UserId, "old@example.com", "oldHash");
        credential.UpdatePasswordHash("newHash");

        // Act
        await repository.UpdateAsync(credential);

        // Assert
        // 1. DB 확인 (새 context 사용)
        using var assertContext = _fixture.CreateDbContext();
        var dbCredential = await assertContext.UserCredentials.FindAsync(user.UserId);
        Assert.Equal("newHash", dbCredential?.PasswordHash);

        // 2. Redis 캐시 삭제 확인
        Assert.False(await db.KeyExistsAsync(RedisKeys.UserCredential(user.UserId)));
        Assert.False(await db.KeyExistsAsync(RedisKeys.UserCredentialEmailMapping("old@example.com")));
    }

    [Fact]
    public async Task Delete_ShouldRemoveFromDbAndClearCache()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var userRepo = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var user = await userRepo.CreateAsync();

        var repository = new UserCredentialRepository(_fixture.RedisConnection, context, NullLogger<UserCredentialRepository>.Instance);
        var db = _fixture.RedisConnection.GetDatabase();

        await repository.CreateAsync(user.UserId, "delete@example.com", "hash");

        // Act
        await repository.RemoveAsync(user.UserId);

        // Assert
        var dbCredential = await context.UserCredentials.FindAsync(user.UserId);
        Assert.Null(dbCredential);

        Assert.False(await db.KeyExistsAsync(RedisKeys.UserCredential(user.UserId)));
        Assert.False(await db.KeyExistsAsync(RedisKeys.UserCredentialEmailMapping("delete@example.com")));
    }

    [Fact]
    public async Task 직전_세대_리프레시_토큰_기록은_해시와_회전시각을_그대로_되돌린다()
    {
        using var context = _fixture.CreateDbContext();
        var userRepo = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var user = await userRepo.CreateAsync();

        var repository = new UserCredentialRepository(_fixture.RedisConnection, context, NullLogger<UserCredentialRepository>.Instance);
        var db = _fixture.RedisConnection.GetDatabase();

        // 실제 저장 형식(헥사 해시)과 같게 — 값 안에 ':' 가 없어야 파싱이 성립하는지까지 본다.
        var hashedToken = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData("token-and-device"u8.ToArray()));
        var rotatedAt = new DateTime(2026, 8, 23, 1, 2, 3, DateTimeKind.Utc);

        await repository.SetPreviousRefreshTokenAsync(user.UserId, hashedToken, rotatedAt, TimeSpan.FromHours(24));

        var restored = await repository.GetPreviousRefreshTokenAsync(user.UserId);
        Assert.NotNull(restored);
        Assert.Equal(hashedToken, restored!.HashedToken);
        Assert.Equal(rotatedAt, restored.RotatedAt);

        var ttl = await db.KeyTimeToLiveAsync(RedisKeys.UserRefreshTokenPrevious(user.UserId));
        Assert.NotNull(ttl);
        Assert.True(ttl!.Value.TotalHours > 23);

        await repository.ClearPreviousRefreshTokenAsync(user.UserId);
        Assert.Null(await repository.GetPreviousRefreshTokenAsync(user.UserId));
    }

    [Fact]
    public async Task 생성_실패는_같은_DbContext의_다음_저장을_오염시키지_않는다()
    {
        // 회귀 고정: 실패한 Insert 가 Added 상태로 남으면, 이어지는 롤백(SaveChanges)이
        // 그 Insert 를 다시 커밋하려다 같은 예외를 던진다.
        // → 원래 실패 사유가 INTERNAL_SERVER_ERROR 로 뭉개지고 고아 레코드가 남는다.
        using var context = _fixture.CreateDbContext();
        var userRepo = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var profileRepo = new UserProfileRepository(_fixture.RedisConnection, context, NullLogger<UserProfileRepository>.Instance);
        var repository = new UserCredentialRepository(_fixture.RedisConnection, context, NullLogger<UserCredentialRepository>.Instance);

        var occupant = await userRepo.CreateAsync();
        await repository.CreateAsync(occupant.UserId, "collide@example.com", "hash");

        var victim = await userRepo.CreateAsync();
        await profileRepo.CreateAsync(victim.UserId, victim.PublicId);

        await Assert.ThrowsAnyAsync<Exception>(
            () => repository.CreateAsync(victim.UserId, "collide@example.com", "hash"));

        // AccountService.RegisterAsync 의 롤백 경로와 동일한 순서
        await profileRepo.RemoveAsync(victim.UserId);
        await userRepo.RemoveAsync(victim.UserId);

        Assert.Null(await context.UserProfiles.AsNoTracking().SingleOrDefaultAsync(up => up.UserId == victim.UserId));
    }

    [Fact]
    public async Task TTL_ShouldBeSetOnCacheKeys()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var userRepo = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var user = await userRepo.CreateAsync();

        var repository = new UserCredentialRepository(_fixture.RedisConnection, context, NullLogger<UserCredentialRepository>.Instance);
        var db = _fixture.RedisConnection.GetDatabase();

        await repository.CreateAsync(user.UserId, "ttl@example.com", "hash");
        var cacheKey = RedisKeys.UserCredential(user.UserId);
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
