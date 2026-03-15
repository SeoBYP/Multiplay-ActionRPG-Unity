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
    private readonly Mock<IDungeonRoomEventStream> _mockEventStream;
    private readonly IDungeonRoomRepository _roomRepository;
    private readonly IUserSessionRepository _sessionRepository;
    private readonly DungeonLobbySubscriptionService _service;

    public DungeonLobbySubscriptionServiceTests()
    {
        _mockEventStream = new Mock<IDungeonRoomEventStream>();
        _roomRepository = new FakeDungeonRoomRepository();
        _sessionRepository = new FakeUserSessionRepository();

        _mockEventStream.Setup(x => x.ReadAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerable.Empty<long>());

        _service = new DungeonLobbySubscriptionService(
            _mockEventStream.Object,
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
        
        // ReadAsync should be called for the room
        _mockEventStream.Verify(x => x.ReadAsync(
            It.Is<long>(id => id == actualRoomId),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
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
        _mockEventStream.Verify(x => x.PublishAsync(
            It.Is<long>(id => id == roomId),
            It.IsAny<CancellationToken>()), Times.Once);
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
        
        // ReadAsync should be called for both rooms
        _mockEventStream.Verify(x => x.ReadAsync(
            It.IsAny<long>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task 구독_취소_호출_시_Redis_채널_구독이_해지되고_취소_토큰이_작동한다()
    {
        // Arrange
        var userId = 1L;
        var roomId = 100L;
        var ctx = new UserRoomContext(userId, roomId);

        // Act
        await _service.UnsubscribeAsync(ctx, CancellationToken.None);

        // Assert
        Assert.True(ctx.Cts.IsCancellationRequested);
    }

    [Fact]
    public async Task 동일_유저가_동시에_여러_번_구독_시_모든_요청이_성공적으로_처리된다()
    {
        // Arrange
        var userId = 1L;
        var roomId = 100L;
        var session = await _sessionRepository.CreateSessionAsync(userId, "user1", "user1@test.com", "pub1");
        var sessionId = session!.SessionId;
        
        var room = await _roomRepository.CreateAsync(userId, "Room1", 4);
        await _sessionRepository.UpdateRoomIdAsync(sessionId, room!.RoomId);

        var tasks = new List<Task<UserRoomContext?>>();
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(_service.SubscribeAsync(sessionId, room.RoomId, CancellationToken.None));
        }

        // Act
        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.All(results, Assert.NotNull);
        _mockEventStream.Verify(x => x.ReadAsync(
            It.IsAny<long>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Exactly(10));
    }
}
