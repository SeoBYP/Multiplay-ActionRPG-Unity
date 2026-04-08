using GameServer.Application.Domains.DungeonLobby;
using GameServer.Application.Domains.DungeonLobby.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GameServer.Tests.Application.Services;

public class DungeonLobbySubscriptionServiceTests
{
    private readonly Mock<IDungeonRoomEventStream> _mockEventStream = new();
    private readonly DungeonLobbySubscriptionService _service;

    public DungeonLobbySubscriptionServiceTests()
    {
        _mockEventStream.Setup(x => x.ReadAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerable.Empty<long>());

        _service = new DungeonLobbySubscriptionService(
            _mockEventStream.Object,
            NullLogger<DungeonLobbySubscriptionService>.Instance);
    }

    [Fact]
    public async Task Subscribe_구독_성공()
    {
        var userId = 1L;
        var roomId = 100L;
        var readCalled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _mockEventStream.Setup(x => x.ReadAsync(roomId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => readCalled.TrySetResult())
            .Returns(AsyncEnumerable.Empty<long>());

        var result = await _service.SubscribeAsync(userId, roomId, CancellationToken.None);
        await readCalled.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.NotNull(result);
        Assert.Equal(userId, result.UserId);
        Assert.Equal(roomId, result.RoomId);
    }

    [Fact]
    public async Task Publish_이벤트_스트림에_게시_호출_확인()
    {
        await _service.PublishAsync(100L, CancellationToken.None);

        _mockEventStream.Verify(x => x.PublishAsync(100L, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Unsubscribe_컨텍스트_취소_확인()
    {
        var ctx = new UserRoomContext(1L, 100L);

        await _service.UnsubscribeAsync(ctx, CancellationToken.None);

        Assert.True(ctx.Cts.IsCancellationRequested);
    }
}
