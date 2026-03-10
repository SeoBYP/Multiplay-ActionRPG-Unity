using GameServer.Application.Domains.DungeonLobby;
using GameServer.Application.Domains.DungeonLobby.Interfaces;
using GameServer.Application.Domains.User.Interfaces;
using GameServer.Domain.Entities;
using GameServer.Tests.Fakes;
using GameServer.Tests.Infrastructure;
using Moq;
using StackExchange.Redis;

namespace GameServer.Tests.Application.Services;

public class DungeonLobbySubscriptionServiceTests
{
    private readonly Mock<IConnectionMultiplexer> _mockRedis;
    private readonly Mock<ISubscriber> _mockSubscriber;
    private readonly IDungeonRoomRepository _roomRepository;
    private readonly IUserSessionRepository _sessionRepository;
    private readonly DungeonLobbySubscriptionService _service;

    public DungeonLobbySubscriptionServiceTests()
    {
        _mockRedis = new Mock<IConnectionMultiplexer>();
        _mockSubscriber = new Mock<ISubscriber>();
        _roomRepository = new FakeDungeonRoomRepository();
        _sessionRepository = new FakeUserSessionRepository();

        _mockRedis.Setup(x => x.GetSubscriber(It.IsAny<object>())).Returns(_mockSubscriber.Object);

        _service = new DungeonLobbySubscriptionService(
            _mockRedis.Object,
            _roomRepository,
            _sessionRepository);
    }

    [Fact]
    public async Task 방_구독_성공_시_Redis_채널_구독을_확인한다()
    {
        // Arrange
        var userId = 1L;
        var roomId = 100L;
        var session = await _sessionRepository.CreateSessionAsync(userId, "user1", "user1@test.com", "pub1");
        var sessionId = session!.SessionId;
        
        var room = await _roomRepository.CreateAsync(userId, "Room1", 4);
        // FakeDungeonRoomRepository creates room with incremented ID, but let's just get it
        var actualRoomId = room!.RoomId;
        
        await _sessionRepository.UpdateRoomIdAsync(sessionId, actualRoomId);

        // Act
        var result = await _service.SubscribeAsync(sessionId, actualRoomId, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(userId, result.UserId);
        Assert.Equal(actualRoomId, result.RoomId);
        _mockSubscriber.Verify(x => x.SubscribeAsync(
            It.Is<RedisChannel>(c => c == RoomChannels.RoomChannel(actualRoomId)),
            It.IsAny<Action<RedisChannel, RedisValue>>(),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task 유효하지_않은_세션으로_구독_시도_시_실패한다()
    {
        // Act
        var result = await _service.SubscribeAsync("invalid-session", 100L, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task 존재하지_않는_방에_대해_구독_시도_시_실패한다()
    {
        // Arrange
        var session = await _sessionRepository.CreateSessionAsync(1, "user1", "user1@test.com", "pub1");
        
        // Act
        var result = await _service.SubscribeAsync(session!.SessionId, 999L, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task 유저가_해당_방의_멤버가_아닌_경우_구독에_실패한다()
    {
        // Arrange
        var userId = 1L;
        var session = await _sessionRepository.CreateSessionAsync(userId, "user1", "user1@test.com", "pub1");
        var room = await _roomRepository.CreateAsync(2L, "Room1", 4); // Host is 2
        
        // 유저는 방에 들어가지 않음

        // Act
        var result = await _service.SubscribeAsync(session!.SessionId, room!.RoomId, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task 방_상태_변경_메시지_발행_시_Redis_채널로_올바른_데이터가_전송된다()
    {
        // Arrange
        var roomId = 100L;

        // Act
        await _service.PublishAsync(roomId, CancellationToken.None);

        // Assert
        _mockSubscriber.Verify(x => x.PublishAsync(
            It.Is<RedisChannel>(c => c == RoomChannels.RoomChannel(roomId)),
            It.Is<RedisValue>(v => (long)v == roomId),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task 이미_구독_중인_상태에서_새로운_방_구독_시_기존_구독이_취소된다()
    {
        // Arrange
        var userId = 1L;
        var session = await _sessionRepository.CreateSessionAsync(userId, "user1", "user1@test.com", "pub1");
        var sessionId = session!.SessionId;
        
        var room1 = await _roomRepository.CreateAsync(userId, "Room1", 4);
        var room2 = await _roomRepository.CreateAsync(userId, "Room2", 4);
        
        await _sessionRepository.UpdateRoomIdAsync(sessionId, room1!.RoomId);
        await _service.SubscribeAsync(sessionId, room1.RoomId, CancellationToken.None);
        
        await _sessionRepository.UpdateRoomIdAsync(sessionId, room2!.RoomId);

        // Act
        var result = await _service.SubscribeAsync(sessionId, room2.RoomId, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(room2.RoomId, result.RoomId);
        
        // UnsubscribeAllAsync should be called for the first room
        _mockSubscriber.Verify(x => x.UnsubscribeAllAsync(It.IsAny<CommandFlags>()), Times.Once);
        // SubscribeAsync should be called twice (once for each room)
        _mockSubscriber.Verify(x => x.SubscribeAsync(
            It.IsAny<RedisChannel>(),
            It.IsAny<Action<RedisChannel, RedisValue>>(),
            It.IsAny<CommandFlags>()), Times.Exactly(2));
    }

    [Fact]
    public async Task 구독_취소_호출_시_Redis_채널_구독이_해지되고_취소_토큰이_작동한다()
    {
        // Arrange
        var userId = 1L;
        var roomId = 100L;
        var ctx = new UserRoomContext(userId, roomId, _mockSubscriber.Object);

        // Act
        await _service.UnsubscribeAsync(ctx, CancellationToken.None);

        // Assert
        _mockSubscriber.Verify(x => x.UnsubscribeAllAsync(It.IsAny<CommandFlags>()), Times.Once);
        Assert.True(ctx.Cts.IsCancellationRequested);
    }
}
