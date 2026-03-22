using System.Net;
using GameServer.Application.Common;
using GameServer.Application.Domains.Chat;
using GameServer.Application.Domains.Chat.Interfaces;
using GameServer.Application.Domains.User.Interfaces;
using GameServer.Domain.Entities.Chat;
using GameServer.Tests.Infrastructure;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace GameServer.Tests.Application.Services;

public class ChatServiceTests
{
    private readonly IChatMessageRepository _chatRepo;
    private readonly IUserSessionRepository _sessionRepo;
    private readonly Mock<IChatEventStream> _mockEventStream;
    private readonly IChatService _service;

    public ChatServiceTests()
    {
        _chatRepo = new FakeChatMessageRepository();
        _sessionRepo = new FakeUserSessionRepository();
        _mockEventStream = new Mock<IChatEventStream>();

        _service = new ChatService(
            _chatRepo,
            _sessionRepo,
            _mockEventStream.Object);
    }

    private async Task<string> CreateSessionAsync(long userId = 100, string nickname = "test",
        string userEmail = "test@test.com", string publicId = "public123")
    {
        var session = await _sessionRepo.CreateSessionAsync(userId, nickname,userEmail, publicId);
        return session!.SessionId;
    }
    
    [Fact]
    public async Task 전역_채팅_메시지_전송_시_성공적으로_저장되고_Redis_글로벌_채널에_발행된다()
    {
        var sessionId = await CreateSessionAsync(1, "Alice");
    
        var result = await _service.SendMessageAsync(sessionId, "hello world", null);
    
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.True(result.Value!.MessageId > 0);
        Assert.Equal(ChatType.Global, result.Value.ChatType);
        // Redis Publish 확인
        _mockEventStream.Verify(x => x.PublishAsync(
            It.Is<string>(c => c == ChatChannels.GlobalChannel),
            It.IsAny<ChatMessage>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [Fact]
    public async Task 방_채팅_메시지_전송_시_해당_방의_ID가_포함되어_저장되고_전용_방_채널에_발행된다()
    {
        var sessionId = await CreateSessionAsync(2, "Bob");
        long roomId = 777;
        await _sessionRepo.UpdateRoomIdAsync(sessionId, roomId); // Room ID 설정 필요
    
        var result = await _service.SendMessageAsync(sessionId, "room msg", null);
    
        Assert.True(result.IsSuccess);
        Assert.Equal(ChatType.Room, result.Value!.ChatType);
        Assert.Equal(roomId, result.Value.RoomId);
        _mockEventStream.Verify(x => x.PublishAsync(
            It.Is<string>(c => c == ChatChannels.RoomChannel(roomId)),
            It.IsAny<ChatMessage>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [Fact]
    public async Task 특정_대상에게_귓속말_전송_시_대상_닉네임이_기록되고_개인_채널에_발행된다()
    {
        var sessionId = await CreateSessionAsync(3, "Carol");
        string targetNickname = "TargetUser";
    
        var result = await _service.SendMessageAsync(sessionId, "psst", targetNickname);
    
        Assert.True(result.IsSuccess);
        Assert.Equal(ChatType.Whisper, result.Value!.ChatType);
        Assert.Equal(targetNickname, result.Value.TargetUserNickName);
        _mockEventStream.Verify(x => x.PublishAsync(
            It.Is<string>(c => c == ChatChannels.WhisperChannel(targetNickname)),
            It.IsAny<ChatMessage>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [Fact]
    public async Task 유효하지_않은_세션으로_채팅_메시지_전송_시도_시_실패하고_발행되지_않는다()
    {
        var result = await _service.SendMessageAsync("invalid-session", "hi", null);
    
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.InvalidRequest, result.InternalErrorCode);
        _mockEventStream.Verify(x => x.PublishAsync(
            It.IsAny<string>(),
            It.IsAny<ChatMessage>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
    
    [Fact]
    public async Task 메시지_고유_ID를_통해_저장된_채팅_내용을_성공적으로_조회한다()
    {
        var sessionId = await CreateSessionAsync(10, "Dave");
        var send = await _service.SendMessageAsync(sessionId, "hello id", null);
        var id = send.Value!.MessageId;
    
        var get = await _service.GetMessageByIdAsync(sessionId, id);
    
        Assert.True(get.IsSuccess);
        Assert.Equal(id, get.Value!.MessageId);
    }
    
    [Fact]
    public async Task 존재하지_않는_메시지_ID로_조회_시_메시지를_찾을_수_없음_에러를_반환한다()
    {
        var sessionId = await CreateSessionAsync(11, "Eve");
    
        var get = await _service.GetMessageByIdAsync(sessionId, 123456789);
    
        Assert.False(get.IsSuccess);
        Assert.Equal(ErrorCodes.MessageNotFound, get.InternalErrorCode);
    }
    
    [Fact]
    public async Task 특정_방의_채팅_내역_조회_시_지정한_개수만큼_최신순으로_정렬되어_반환된다()
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
    public async Task 특정_사용자가_작성한_메시지_목록_조회_시_지정한_개수만큼_최신순으로_정렬되어_반환된다()
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
    public async Task 주어진_메시지_ID_이후에_발생한_메시지들_중_사용자가_볼_수_있는_채팅만_필터링하여_조회한다()
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
    public async Task 중간_지점의_메시지_ID를_기준으로_그_이후의_메시지가_정상적으로_조회되는지_확인한다()
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
    public async Task 유효하지_않은_세션으로_메시지_이후_조회_시_빈_목록을_반환한다()
    {
        var results = await _service.GetMessagesAfterAsync("invalid-session", 0);
        Assert.Empty(results);
    }

    [Fact]
    public async Task 유저가_어떤_방에도_속해있지_않을_때_메시지_조회_시_방_채팅은_제외하고_반환한다()
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

    [Fact]
    public async Task 동일_세션에서_동시에_여러_메시지_전송_시_동시성_제어가_작동하여_모두_성공한다()
    {
        var sessionId = await CreateSessionAsync(400, "ConcurrentUser");
        var tasks = new List<Task<Result<ChatMessage>>>();

        for (int i = 0; i < 10; i++)
        {
            tasks.Add(_service.SendMessageAsync(sessionId, $"msg-{i}", null));
        }

        var results = await Task.WhenAll(tasks);

        Assert.All(results, r => Assert.True(r.IsSuccess));
        _mockEventStream.Verify(x => x.PublishAsync(
            It.IsAny<string>(),
            It.IsAny<ChatMessage>(),
            It.IsAny<CancellationToken>()), Times.Exactly(10));
    }

    [Fact]
    public async Task 서로_다른_여러_유저가_동시에_메시지_전송_시_모두_성공한다()
    {
        int userCount = 10;
        var sessions = new List<string>();
        for (int i = 0; i < userCount; i++)
        {
            sessions.Add(await CreateSessionAsync(1000 + i, $"User-{i}"));
        }

        var tasks = sessions.Select(s => _service.SendMessageAsync(s, "hello", null)).ToList();

        var results = await Task.WhenAll(tasks);

        Assert.All(results, r => Assert.True(r.IsSuccess));
        _mockEventStream.Verify(x => x.PublishAsync(
            It.IsAny<string>(),
            It.IsAny<ChatMessage>(),
            It.IsAny<CancellationToken>()), Times.Exactly(userCount));
    }
}
