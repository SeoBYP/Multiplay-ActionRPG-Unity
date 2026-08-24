using GameServer.Domain.Entities.Chat;
using GameServer.Infrastructure.Domains;
using GameServer.Infrastructure.Domains.Chat;
using GameServer.Tests.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;

namespace GameServer.Tests.Infrastructure.Integrations;

[Collection("RepositoryIntegrationTests")]
public class ChatMessageRepositoryIntegrationTests(RepositoryTestFixture fixture)
{
    private readonly RepositoryTestFixture _fixture = fixture;

    [Fact]
    public async Task Create_ShouldInsertToDbAndCacheInRedis()
    {
        // Arrange
        var repository = CreateRepository();
        string sender = "TestUser";
        string messageContent = "Hello, World!";
        ChatType chatType = ChatType.Global;

        // Act
        var created = await repository.CreateAsync(sender, chatType, messageContent, null, null);

        // Assert - DB
        using (var context = _fixture.CreateDbContext())
        {
            var dbMessage = await context.ChatMessages.FindAsync(created.MessageId);
            Assert.NotNull(dbMessage);
            Assert.Equal(sender, dbMessage.SenderUserNickName);
            Assert.Equal(messageContent, dbMessage.Message);
        }

        // Assert - Redis (Hash)
        var redis = _fixture.RedisConnection.GetDatabase();
        var hashEntries = await redis.HashGetAllAsync(RedisKeys.ChatMessage(created.MessageId));
        Assert.NotEmpty(hashEntries);
        var dict = hashEntries.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());
        Assert.Equal(sender, dict["SenderName"]);
        Assert.Equal(messageContent, dict["Message"]);

        // Assert - Redis (SortedSet Index)
        var allMessages = await redis.SortedSetRangeByScoreAsync(RedisKeys.ChatAllMessages());
        Assert.Contains(created.MessageId, allMessages.Select(v => (long)v));
    }

    [Fact]
    public async Task Read_Hit_ShouldReturnFromCacheWithoutDb()
    {
        // Arrange
        var repository = CreateRepository();
        var created = await repository.CreateAsync("User1", ChatType.Global, "Msg1", null, null);

        // DB에서 데이터 강제 삭제하여 Redis 캐시만 남김
        using (var context = _fixture.CreateDbContext())
        {
            var dbMsg = await context.ChatMessages.FindAsync(created.MessageId);
            context.ChatMessages.Remove(dbMsg!);
            await context.SaveChangesAsync();
        }

        // Act
        var result = await repository.GetMessageByIdAsync(created.MessageId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(created.MessageId, result.MessageId);
        Assert.Equal("Msg1", result.Message);
    }

    [Fact]
    public async Task Read_Miss_ShouldFetchFromDbAndRecache()
    {
        // Arrange
        var repository = CreateRepository();
        var created = await repository.CreateAsync("User2", ChatType.Global, "Msg2", null, null);
        var redis = _fixture.RedisConnection.GetDatabase();

        // 캐시 삭제
        await redis.KeyDeleteAsync(RedisKeys.ChatMessage(created.MessageId));

        // Act
        var result = await repository.GetMessageByIdAsync(created.MessageId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Msg2", result.Message);

        // 캐시 재구성 확인
        var hashEntries = await redis.HashGetAllAsync(RedisKeys.ChatMessage(created.MessageId));
        Assert.NotEmpty(hashEntries);
    }

    [Fact]
    public async Task Delete_ShouldRemoveFromDbAndRedis()
    {
        // Arrange
        var repository = CreateRepository();
        var created = await repository.CreateAsync("User3", ChatType.Global, "Msg3", null, null);
        var redis = _fixture.RedisConnection.GetDatabase();

        // Act
        var success = await repository.DeleteAsync(created.MessageId);

        // Assert
        Assert.True(success);

        // DB 확인
        using (var context = _fixture.CreateDbContext())
        {
            var dbMsg = await context.ChatMessages.FindAsync(created.MessageId);
            Assert.Null(dbMsg);
        }

        // Redis 확인
        var exists = await redis.KeyExistsAsync(RedisKeys.ChatMessage(created.MessageId));
        Assert.False(exists);

        var allMessages = await redis.SortedSetRangeByScoreAsync(RedisKeys.ChatAllMessages());
        Assert.DoesNotContain(created.MessageId, allMessages.Select(v => (long)v));
    }

    [Fact]
    public async Task TTL_ShouldBeSetInRedis()
    {
        // Arrange
        var repository = CreateRepository();
        var created = await repository.CreateAsync("User4", ChatType.Global, "Msg4", null, null);
        var redis = _fixture.RedisConnection.GetDatabase();

        // Act
        var ttl = await redis.KeyTimeToLiveAsync(RedisKeys.ChatMessage(created.MessageId));

        // Assert
        Assert.NotNull(ttl);
        Assert.True(ttl > TimeSpan.Zero);
        Assert.True(ttl <= RedisSettings.RedisCacheTtl);
    }

    [Fact]
    public async Task GetMessagesAfterAsync_ShouldHandleCacheAndFallback()
    {
        // Arrange
        var repository = CreateRepository();
        var msg1 = await repository.CreateAsync("User1", ChatType.Global, "First", null, null);
        var msg2 = await repository.CreateAsync("User1", ChatType.Global, "Second", null, null);
        var redis = _fixture.RedisConnection.GetDatabase();

        // 1. 캐시 히트 테스트 (msg2 이후 메시지 조회 - 현재는 없음)
        var results = await repository.GetMessagesAfterAsync(msg1.MessageId);
        Assert.Single(results);
        Assert.Equal(msg2.MessageId, results.First().MessageId);

        // 2. 캐시 미스 (DB 폴백) 테스트
        await redis.KeyDeleteAsync(RedisKeys.ChatAllMessages());
        var resultsFromDb = await repository.GetMessagesAfterAsync(msg1.MessageId);
        Assert.Single(resultsFromDb);
        Assert.Equal("Second", resultsFromDb.First().Message);

        // 캐시 재구성 확인
        var indexExists = await redis.KeyExistsAsync(RedisKeys.ChatAllMessages());
        Assert.True(indexExists);
    }

    [Fact]
    public async Task 인덱스가_옛_구간을_잃어도_이력이_누락되지_않는다()
    {
        var repository = CreateRepository();
        var redis = _fixture.RedisConnection.GetDatabase();

        var m1 = await repository.CreateAsync("IdxUser", ChatType.Global, "1st", null, null);
        var m2 = await repository.CreateAsync("IdxUser", ChatType.Global, "2nd", null, null);
        var m3 = await repository.CreateAsync("IdxUser", ChatType.Global, "3rd", null, null);

        // 인덱스(전체 컬렉션 TTL)가 만료된 뒤 새 메시지 하나로 "부활" 한 상태를 재현.
        await redis.KeyDeleteAsync(RedisKeys.ChatAllMessages());
        var m4 = await repository.CreateAsync("IdxUser", ChatType.Global, "4th", null, null);

        var results = (await repository.GetMessagesAfterAsync(m1.MessageId)).ToList();

        // 인덱스에는 m4 밖에 없지만, 진실원(DB)에는 m2·m3 가 있다.
        // 인덱스가 경계(m1)를 덮지 못하면 DB 로 폴백해야 한다 — 아니면 이력이 조용히 빈다.
        Assert.Equal(
            new[] { m2.MessageId, m3.MessageId, m4.MessageId },
            results.Select(r => r.MessageId).OrderBy(x => x).ToArray());
    }

    [Fact]
    public async Task 메시지_해시가_먼저_만료돼도_인덱스에_있는_메시지는_DB로_보충된다()
    {
        var repository = CreateRepository();
        var redis = _fixture.RedisConnection.GetDatabase();

        var m1 = await repository.CreateAsync("HashUser", ChatType.Global, "base", null, null);
        var m2 = await repository.CreateAsync("HashUser", ChatType.Global, "target", null, null);

        // 인덱스 TTL 은 메시지 추가마다 갱신되지만 메시지 해시 TTL 은 생성 시 1회뿐 →
        // 해시가 먼저 만료돼 인덱스에만 id 가 남는 상태가 상시 발생한다.
        await redis.KeyDeleteAsync(RedisKeys.ChatMessage(m2.MessageId));

        var results = (await repository.GetMessagesAfterAsync(m1.MessageId)).ToList();

        Assert.Contains(results, r => r.MessageId == m2.MessageId);
        Assert.Equal("target", results.Single(r => r.MessageId == m2.MessageId).Message);
    }

    private ChatMessageRepository CreateRepository()
    {
        return new ChatMessageRepository(
            _fixture.RedisConnection,
            _fixture.CreateDbContext(),
            NullLogger<ChatMessageRepository>.Instance);
    }
}
