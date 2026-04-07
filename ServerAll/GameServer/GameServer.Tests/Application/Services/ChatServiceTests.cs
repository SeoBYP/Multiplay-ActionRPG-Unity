using GameServer.Application.Common;
using GameServer.Application.Common.Interfaces;
using GameServer.Application.Domains.Chat;
using GameServer.Application.Domains.Chat.Interfaces;
using GameServer.Application.Domains.DungeonLobby.Interfaces;
using GameServer.Application.Domains.User.Interfaces;
using GameServer.Domain.Entities.Chat;
using GameServer.Tests.Infrastructure.Fakes.Repositories;
using Moq;

namespace GameServer.Tests.Application.Services;

public class ChatServiceTests
{
    private readonly IChatMessageRepository _chatRepo;
    private readonly IUserSessionRepository _sessionRepo;
    private readonly FakeUserProfileRepository _profileRepo;
    private readonly IDungeonRoomRepository _roomRepo;
    private readonly FakeDungeonRoomPlayerRepository _roomPlayerRepo;
    private readonly Mock<IChatEventStream> _mockEventStream;
    private readonly IProfanityFilter _profanityFilter;
    private readonly IUserLock _userLock;
    private readonly IChatService _service;

    public ChatServiceTests()
    {
        _chatRepo = new FakeChatMessageRepository();
        _sessionRepo = new FakeUserSessionRepository();
        _profileRepo = new FakeUserProfileRepository();
        _roomRepo = new FakeDungeonRoomRepository();
        _roomPlayerRepo = new FakeDungeonRoomPlayerRepository();
        _mockEventStream = new Mock<IChatEventStream>();
        _profanityFilter = new PassThroughProfanityFilter();
        _userLock = new NoOpUserLock();

        _service = new ChatService(
            _chatRepo,
            _sessionRepo,
            _profileRepo,
            _roomRepo,
            _roomPlayerRepo,
            _profanityFilter,
            _userLock,
            _mockEventStream.Object);
    }

    private async Task<string> CreateSessionAsync(long userId = 100, string nickname = "test")
    {
        await _profileRepo.CreateAsync(userId, nickname);
        var session = await _sessionRepo.CreateSessionAsync(userId);
        return session!.SessionId;
    }

    [Fact]
    public async Task SendGlobalMessage_전체_채팅_게시_확인()
    {
        var sessionId = await CreateSessionAsync(1, "Alice");

        var result = await _service.SendMessageAsync(sessionId, "hello world", null);

        Assert.True(result.IsSuccess);
        Assert.Equal(ChatType.Global, result.Value!.ChatType);
        _mockEventStream.Verify(x => x.PublishAsync(
            ChatChannels.GlobalChannel,
            It.IsAny<ChatMessage>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendRoomMessage_방_채팅_게시_확인()
    {
        var sessionId = await CreateSessionAsync(2, "Bob");
        var room = await _roomRepo.CreateAsync(2, "room", 4);
        await _roomPlayerRepo.CreateAsync(room!.RoomId, 2);

        var result = await _service.SendMessageAsync(sessionId, "room msg", null);

        Assert.True(result.IsSuccess);
        Assert.Equal(ChatType.Room, result.Value!.ChatType);
        Assert.Equal(room!.RoomId, result.Value.RoomId);
        _mockEventStream.Verify(x => x.PublishAsync(
            ChatChannels.RoomChannel(room.RoomId),
            It.IsAny<ChatMessage>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendWhisper_귓속말_게시_확인()
    {
        var sessionId = await CreateSessionAsync(3, "Carol");

        var result = await _service.SendMessageAsync(sessionId, "psst", "TargetUser");

        Assert.True(result.IsSuccess);
        Assert.Equal(ChatType.Whisper, result.Value!.ChatType);
        Assert.Equal("TargetUser", result.Value.TargetUserNickName);
        _mockEventStream.Verify(x => x.PublishAsync(
            ChatChannels.WhisperChannel("TargetUser"),
            It.IsAny<ChatMessage>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InvalidSession_메시지_전송_실패_확인()
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
    public async Task SendMessage_메시지가_너무_길면_게시_없이_실패()
    {
        var sessionId = await CreateSessionAsync(4, "LongUser");
        var message = new string('a', ChatMessage.MaxMessageLength + 1);

        var result = await _service.SendMessageAsync(sessionId, message, null);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.InvalidRequest, result.InternalErrorCode);
        _mockEventStream.Verify(x => x.PublishAsync(
            It.IsAny<string>(),
            It.IsAny<ChatMessage>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetMessageById_저장된_메시지_반환_확인()
    {
        var sessionId = await CreateSessionAsync(10, "Dave");
        var send = await _service.SendMessageAsync(sessionId, "hello id", null);

        var get = await _service.GetMessageByIdAsync(sessionId, send.Value!.MessageId);

        Assert.True(get.IsSuccess);
        Assert.Equal(send.Value.MessageId, get.Value!.MessageId);
    }

    [Fact]
    public async Task MissingMessage_메시지_없음_반환_확인()
    {
        var sessionId = await CreateSessionAsync(11, "Eve");

        var get = await _service.GetMessageByIdAsync(sessionId, 123456789);

        Assert.False(get.IsSuccess);
        Assert.Equal(ErrorCodes.MessageNotFound, get.InternalErrorCode);
    }

    [Fact]
    public async Task GetMessagesByRoom_최신_방_메시지_반환_확인()
    {
        var sessionId = await CreateSessionAsync(20, "RoomUser");
        var room = await _roomRepo.CreateAsync(20, "room", 4);
        await _roomPlayerRepo.CreateAsync(room!.RoomId, 20);

        for (int i = 0; i < 5; i++)
            await _service.SendMessageAsync(sessionId, $"msg-{i}", null);

        var listResult = await _service.GetMessagesByRoomAsync(sessionId, room!.RoomId, limit: 3);

        Assert.True(listResult.IsSuccess);
        Assert.Equal(3, listResult.Value!.Count);
    }

    [Fact]
    public async Task GetMessagesByUser_최신_사용자_메시지_반환_확인()
    {
        var sessionId = await CreateSessionAsync(30, "UserA");

        for (int i = 0; i < 3; i++)
            await _service.SendMessageAsync(sessionId, $"g-{i}", null);
        for (int i = 0; i < 2; i++)
            await _service.SendMessageAsync(sessionId, $"w-{i}", "UserB");

        var listResult = await _service.GetMessagesByUserAsync(sessionId, "UserA", limit: 4);

        Assert.True(listResult.IsSuccess);
        Assert.Equal(4, listResult.Value!.Count);
    }

    [Fact]
    public async Task GetMessagesAfter_가시성에_따른_필터링_확인()
    {
        var aliceSessionId = await CreateSessionAsync(100, "Alice");
        var room = await _roomRepo.CreateAsync(100, "room-101", 4);
        await _roomPlayerRepo.CreateAsync(room!.RoomId, 100);

        await _chatRepo.CreateAsync("System", ChatType.Global, "Global Msg", null, null);
        await _chatRepo.CreateAsync("Bob", ChatType.Room, "Room 101 Msg", room.RoomId, null);
        await _chatRepo.CreateAsync("Bob", ChatType.Room, "Room 102 Msg", 102, null);
        await _chatRepo.CreateAsync("Bob", ChatType.Whisper, "To Alice", null, "Alice");
        await _chatRepo.CreateAsync("Alice", ChatType.Whisper, "To Bob", null, "Bob");
        await _chatRepo.CreateAsync("Bob", ChatType.Whisper, "To Carol", null, "Carol");

        var results = (await _service.GetMessagesAfterAsync(aliceSessionId, 0)).ToList();

        Assert.Equal(4, results.Count);
        Assert.Contains(results, m => m.Message == "Global Msg");
        Assert.Contains(results, m => m.Message == "Room 101 Msg");
        Assert.Contains(results, m => m.Message == "To Alice");
        Assert.Contains(results, m => m.Message == "To Bob");
        Assert.DoesNotContain(results, m => m.Message == "Room 102 Msg");
        Assert.DoesNotContain(results, m => m.Message == "To Carol");
    }

    [Fact]
    public async Task InvalidSession_GetMessagesAfter_빈_목록_반환()
    {
        var results = await _service.GetMessagesAfterAsync("invalid-session", 0);
        Assert.Empty(results);
    }

    [Fact]
    public async Task NoRoom_방_메시지_제외_확인()
    {
        var sessionId = await CreateSessionAsync(300, "NoRoomUser");

        await _chatRepo.CreateAsync("System", ChatType.Global, "Global", null, null);
        await _chatRepo.CreateAsync("Bob", ChatType.Room, "Room 101 Msg", 101, null);

        var results = (await _service.GetMessagesAfterAsync(sessionId, 0)).ToList();

        Assert.Single(results);
        Assert.Equal("Global", results[0].Message);
        Assert.DoesNotContain(results, m => m.ChatType == ChatType.Room);
    }

    private sealed class PassThroughProfanityFilter : IProfanityFilter
    {
        public string Filter(string message) => message;

        public bool IsProfane(string message) => false;
    }

    private sealed class NoOpUserLock : IUserLock
    {
        public Task<IAsyncDisposable> AcquireAsync(string lockKey, CancellationToken ct = default)
            => Task.FromResult<IAsyncDisposable>(new Releaser());

        private sealed class Releaser : IAsyncDisposable
        {
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
