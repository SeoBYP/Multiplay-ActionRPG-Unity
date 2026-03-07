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
using Xunit;

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
    
        var result = await _service.SendMessageAsync(sessionId, "hello world", null);
    
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
        await _sessionRepo.UpdateRoomIdAsync(sessionId, roomId); // Room ID 설정 필요
    
        var result = await _service.SendMessageAsync(sessionId, "room msg", null);
    
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
    
        var result = await _service.SendMessageAsync(sessionId, "psst", targetNickname);
    
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
        var result = await _service.SendMessageAsync("invalid-session", "hi", null);
    
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
        var send = await _service.SendMessageAsync(sessionId, "hello id", null);
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
        await _sessionRepo.UpdateRoomIdAsync(sessionId, roomId);
    
        // 5개 전송
        for (int i = 0; i < 5; i++)
            await _service.SendMessageAsync(sessionId, $"msg-{i}", null);
    
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
            await _service.SendMessageAsync(sessionId, $"g-{i}", null);
        for (int i = 0; i < 2; i++)
            await _service.SendMessageAsync(sessionId, $"w-{i}", other);
    
        var listResult = await _service.GetMessagesByUserAsync(sessionId, "UserA", limit: 4);
    
        Assert.True(listResult.IsSuccess);
        Assert.Equal(4, listResult.Value!.Count);
        var ids = listResult.Value!.Select(m => m.MessageId).ToList();
        var sorted = ids.OrderByDescending(x => x).ToList();
        Assert.Equal(sorted, ids);
    }
    
    [Fact]
    public async Task GetMessagesAfterAsync_필터링_확인()
    {
        // 1. 유저 세션 생성 (Alice, Room 101)
        var userId = 100L;
        var aliceNickname = "Alice";
        var roomId = 101L;
        var session = await _sessionRepo.CreateSessionAsync(userId, aliceNickname, "alice@test.com", "pub-alice");
        await _sessionRepo.UpdateRoomIdAsync(session!.SessionId, roomId);
        var sessionId = session.SessionId;

        // 2. 다양한 메시지 생성
        await _chatRepo.CreateAsync("System", ChatType.Global, "Global Msg", null, null); // ID 2 (ID 1은 생성 시 채번 됨)
        await _chatRepo.CreateAsync("Bob", ChatType.Room, "Room 101 Msg", 101, null);    // ID 3
        await _chatRepo.CreateAsync("Bob", ChatType.Room, "Room 102 Msg", 102, null);    // ID 4 (Alice는 못봐야 함)
        await _chatRepo.CreateAsync("Bob", ChatType.Whisper, "To Alice", null, "Alice"); // ID 5
        await _chatRepo.CreateAsync("Alice", ChatType.Whisper, "To Bob", null, "Bob");   // ID 6
        await _chatRepo.CreateAsync("Bob", ChatType.Whisper, "To Carol", null, "Carol"); // ID 7 (Alice는 못봐야 함)

        // 3. LastMessageId = 0으로 조회
        var results = (await _service.GetMessagesAfterAsync(sessionId, 0)).ToList();

        // 4. 검증
        Assert.Equal(4, results.Count);
        Assert.Contains(results, m => m.Message == "Global Msg");
        Assert.Contains(results, m => m.Message == "Room 101 Msg");
        Assert.Contains(results, m => m.Message == "To Alice");
        Assert.Contains(results, m => m.Message == "To Bob");
        Assert.DoesNotContain(results, m => m.Message == "Room 102 Msg");
        Assert.DoesNotContain(results, m => m.Message == "To Carol");
    }

    [Fact]
    public async Task GetMessagesAfterAsync_중간_ID_조회()
    {
        // 1. 유저 세션 생성 (ID 100)
        var sessionId = await CreateSessionAsync(200, "User200");
        
        // 2. 메시지 여러개 생성
        await _chatRepo.CreateAsync("UserA", ChatType.Global, "Msg1", null, null); // ID 2
        await _chatRepo.CreateAsync("UserA", ChatType.Global, "Msg2", null, null); // ID 3
        await _chatRepo.CreateAsync("UserA", ChatType.Global, "Msg3", null, null); // ID 4
        
        // 3. LastMessageId = 3으로 조회
        var results = (await _service.GetMessagesAfterAsync(sessionId, 3)).ToList();
        
        // 4. 검증
        Assert.Single(results);
        Assert.Equal("Msg3", results[0].Message);
        Assert.True(results[0].MessageId > 3);
    }

    [Fact]
    public async Task GetMessagesAfterAsync_세션없음_빈결과()
    {
        var results = await _service.GetMessagesAfterAsync("invalid-session", 0);
        Assert.Empty(results);
    }

    [Fact]
    public async Task GetMessagesAfterAsync_방에_없을_때_방채팅_제외()
    {
        // 1. 유저 세션 생성 (No Room)
        var sessionId = await CreateSessionAsync(300, "NoRoomUser");
        
        // 2. 메시지 생성
        await _chatRepo.CreateAsync("System", ChatType.Global, "Global", null, null);
        await _chatRepo.CreateAsync("Bob", ChatType.Room, "Room 101 Msg", 101, null);
        
        // 3. 조회
        var results = (await _service.GetMessagesAfterAsync(sessionId, 0)).ToList();
        
        // 4. 검증
        Assert.Single(results);
        Assert.Equal("Global", results[0].Message);
        Assert.DoesNotContain(results, m => m.ChatType == ChatType.Room);
    }
}