using System.Net;
using GameServer.Application.Common;
using GameServer.Application.Services.Chat;
using GameServer.Application.Services.Chat.Interfaces;
using GameServer.Domain.Entities.Chat;
using GameServer.Infrastructure.Interfaces.Chat;
using GameServer.Infrastructure.Interfaces.User;
using GameServer.Tests.Infrastructure;
using Moq;
using StackExchange.Redis;

namespace GameServer.Tests.Application.Services;

public class ChatServiceTests
{
    private readonly IChatMessageRepository _chatRepo;
    private readonly IUserSessionRepository _sessionRepo;
    private readonly Mock<IConnectionMultiplexer> _mockRedis;
    private readonly Mock<ISubscriber> _mockSubscriber;
    private readonly IChatService _service;

    public ChatServiceTests()
    {
        _chatRepo = new FakeChatMessageRepository();
        _sessionRepo = new FakeUserSessionRepository();
        _mockRedis = new Mock<IConnectionMultiplexer>();
        _mockSubscriber = new Mock<ISubscriber>();

        _mockRedis.Setup(x => x.GetSubscriber(It.IsAny<object>())).Returns(_mockSubscriber.Object);

        _service = new ChatService(_chatRepo, _sessionRepo, _mockRedis.Object);
    }

    private async Task<string> CreateSessionAsync(long userId = 100, string nickname = "test",
        string userEmail = "test@test.com", string publicId = "public123")
    {
        var session = await _sessionRepo.CreateSessionAsync(userId, nickname,userEmail, publicId);
        return session!.SessionId;
    }

    [Fact]
    public async Task SendMessageAsync_Global_성공_및_퍼블리시()
    {
        var sessionId = await CreateSessionAsync(1, "Alice");

        var result = await _service.SendMessageAsync(sessionId, ChatType.Global, "hello world", null, null);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.True(result.Value!.MessageId > 0);
        Assert.Equal(ChatType.Global, result.Value.ChatType);
        // Redis Publish 확인
        _mockSubscriber.Verify(x => x.PublishAsync(
            It.Is<RedisChannel>(c => c == ChatChannels.GlobalChannel),
            It.IsAny<RedisValue>(),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task SendMessageAsync_Room_성공_및_채널확인()
    {
        var sessionId = await CreateSessionAsync(2, "Bob");
        long roomId = 777;

        var result = await _service.SendMessageAsync(sessionId, ChatType.Room, "room msg", roomId, null);

        Assert.True(result.IsSuccess);
        Assert.Equal(ChatType.Room, result.Value!.ChatType);
        Assert.Equal(roomId, result.Value.RoomId);
        _mockSubscriber.Verify(x => x.PublishAsync(
            It.Is<RedisChannel>(c => c == ChatChannels.RoomChannel(roomId)),
            It.IsAny<RedisValue>(),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task SendMessageAsync_Whisper_성공_및_채널확인()
    {
        var sessionId = await CreateSessionAsync(3, "Carol");
        string targetNickname = "TargetUser";

        var result = await _service.SendMessageAsync(sessionId, ChatType.Whisper, "psst", null, targetNickname);

        Assert.True(result.IsSuccess);
        Assert.Equal(ChatType.Whisper, result.Value!.ChatType);
        Assert.Equal(targetNickname, result.Value.TargetUserNickName);
        _mockSubscriber.Verify(x => x.PublishAsync(
            It.Is<RedisChannel>(c => c == ChatChannels.WhisperChannel(targetNickname)),
            It.IsAny<RedisValue>(),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task SendMessageAsync_세션없음_InvalidRequest()
    {
        var result = await _service.SendMessageAsync("invalid-session", ChatType.Global, "hi", null, null);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.InvalidRequest, result.InternalErrorCode);
        _mockSubscriber.Verify(x => x.PublishAsync(
            It.IsAny<RedisChannel>(),
            It.IsAny<RedisValue>(),
            It.IsAny<CommandFlags>()), Times.Never);
    }

    [Fact]
    public async Task GetMessageByIdAsync_정상조회()
    {
        var sessionId = await CreateSessionAsync(10, "Dave");
        var send = await _service.SendMessageAsync(sessionId, ChatType.Global, "hello id", null, null);
        var id = send.Value!.MessageId;

        var get = await _service.GetMessageByIdAsync(sessionId, id);

        Assert.True(get.IsSuccess);
        Assert.Equal(id, get.Value!.MessageId);
    }

    [Fact]
    public async Task GetMessageByIdAsync_없으면_MessageNotFound()
    {
        var sessionId = await CreateSessionAsync(11, "Eve");

        var get = await _service.GetMessageByIdAsync(sessionId, 123456789);

        Assert.False(get.IsSuccess);
        Assert.Equal(ErrorCodes.MessageNotFound, get.InternalErrorCode);
    }

    [Fact]
    public async Task GetMessagesByRoomAsync_리미트_및_정렬()
    {
        var sessionId = await CreateSessionAsync(20, "RoomUser");
        long roomId = 5555;

        // 5개 전송
        for (int i = 0; i < 5; i++)
            await _service.SendMessageAsync(sessionId, ChatType.Room, $"msg-{i}", roomId, null);

        var listResult = await _service.GetMessagesByRoomAsync(sessionId, roomId, limit: 3);

        Assert.True(listResult.IsSuccess);
        Assert.Equal(3, listResult.Value!.Count);
        // 최신순 정렬(MessageId 내림차순)
        var ids = listResult.Value!.Select(m => m.MessageId).ToList();
        var sorted = ids.OrderByDescending(x => x).ToList();
        Assert.Equal(sorted, ids);
    }

    [Fact]
    public async Task GetMessagesByUserAsync_리미트_및_정렬()
    {
        var sessionId = await CreateSessionAsync(30, "UserA");
        string other = "UserB";

        // UserA Global 3개, whisper 2개
        for (int i = 0; i < 3; i++)
            await _service.SendMessageAsync(sessionId, ChatType.Global, $"g-{i}", null, null);
        for (int i = 0; i < 2; i++)
            await _service.SendMessageAsync(sessionId, ChatType.Whisper, $"w-{i}", null, other);

        var listResult = await _service.GetMessagesByUserAsync(sessionId, "UserA", limit: 4);

        Assert.True(listResult.IsSuccess);
        Assert.Equal(4, listResult.Value!.Count);
        var ids = listResult.Value!.Select(m => m.MessageId).ToList();
        var sorted = ids.OrderByDescending(x => x).ToList();
        Assert.Equal(sorted, ids);
    }

    [Fact]
    public async Task 목록_API_세션없음_InvalidRequest()
    {
        var byRoom = await _service.GetMessagesByRoomAsync("nope", 1);
        var byUser = await _service.GetMessagesByUserAsync("nope", "UserA");

        Assert.False(byRoom.IsSuccess);
        Assert.Equal(ErrorCodes.InvalidRequest, byRoom.InternalErrorCode);
        Assert.False(byUser.IsSuccess);
        Assert.Equal(ErrorCodes.InvalidRequest, byUser.InternalErrorCode);
    }
}