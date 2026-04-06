using GameServer.Application.Domains.DungeonLobby;
using GameServer.Application.Domains.DungeonLobby.Interfaces;
using GameServer.Application.Domains.User.Interfaces;
using GameServer.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GameServer.Tests.Application.Services;

public class DungeonLobbySubscriptionServiceTests
{
    private readonly Mock<IDungeonRoomEventStream> _mockEventStream = new();
    private readonly Mock<IDungeonRoomRepository> _mockRoomRepository = new();
    private readonly Mock<IDungeonRoomPlayerRepository> _mockRoomPlayerRepository = new();
    private readonly Mock<IUserSessionRepository> _mockSessionRepository = new();
    private readonly DungeonLobbySubscriptionService _service;

    public DungeonLobbySubscriptionServiceTests()
    {
        _mockEventStream.Setup(x => x.ReadAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerable.Empty<long>());

        _service = new DungeonLobbySubscriptionService(
            _mockEventStream.Object,
            _mockRoomRepository.Object,
            _mockRoomPlayerRepository.Object,
            _mockSessionRepository.Object,
            NullLogger<DungeonLobbySubscriptionService>.Instance);
    }

    [Fact]
    public async Task Subscribe_SucceedsForRoomMember()
    {
        var userId = 1L;
        var roomId = 100L;
        var sessionId = "test-session";
        var readCalled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var session = UserSession.Create(userId, sessionId);
        var room = DungeonRoom.Create("Room1", userId, 4);
        room.GetType().GetProperty("RoomId")?.SetValue(room, roomId);
        var roomPlayer = DungeonRoomPlayer.Create(roomId, userId);

        _mockSessionRepository.Setup(x => x.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _mockRoomRepository.Setup(x => x.GetByIdAsync(roomId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(room);
        _mockRoomPlayerRepository.Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(roomPlayer);
        _mockEventStream.Setup(x => x.ReadAsync(roomId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => readCalled.TrySetResult())
            .Returns(AsyncEnumerable.Empty<long>());

        var result = await _service.SubscribeAsync(sessionId, roomId, CancellationToken.None);
        await readCalled.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.NotNull(result);
        Assert.Equal(userId, result.UserId);
        Assert.Equal(roomId, result.RoomId);
    }

    [Fact]
    public async Task Subscribe_InvalidSession_ReturnsNull()
    {
        _mockSessionRepository.Setup(x => x.GetBySessionIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserSession?)null);

        var result = await _service.SubscribeAsync("invalid-session", 100L, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Publish_CallsEventStream()
    {
        await _service.PublishAsync(100L, CancellationToken.None);

        _mockEventStream.Verify(x => x.PublishAsync(100L, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Unsubscribe_CancelsContext()
    {
        var ctx = new UserRoomContext(1L, 100L);

        await _service.UnsubscribeAsync(ctx, CancellationToken.None);

        Assert.True(ctx.Cts.IsCancellationRequested);
    }
}
