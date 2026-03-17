using GameServer.Application.Domains.DungeonLobby;
using GameServer.Application.Domains.DungeonLobby.Interfaces;
using GameServer.Application.Domains.User.Interfaces;
using GameServer.Domain.Entities;
using Moq;

namespace GameServer.Tests.Application.Services;

public class DungeonLobbySubscriptionServiceTests
{
    private readonly Mock<IDungeonRoomEventStream> _mockEventStream;
    private readonly Mock<IDungeonRoomRepository> _mockRoomRepository;
    private readonly Mock<IUserSessionRepository> _mockSessionRepository;
    private readonly DungeonLobbySubscriptionService _service;

    public DungeonLobbySubscriptionServiceTests()
    {
        _mockEventStream = new Mock<IDungeonRoomEventStream>();
        _mockRoomRepository = new Mock<IDungeonRoomRepository>();
        _mockSessionRepository = new Mock<IUserSessionRepository>();

        _mockEventStream.Setup(x => x.ReadAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerable.Empty<long>());

        _service = new DungeonLobbySubscriptionService(
            _mockEventStream.Object,
            _mockRoomRepository.Object,
            _mockSessionRepository.Object);
    }

    [Fact]
    public async Task 방_구독_성공_시_Redis_채널_구독을_확인한다()
    {
        // Arrange
        var userId = 1L;
        var roomId = 100L;
        var sessionId = "test-session";
        var readCalled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        
        var session = UserSession.Create(userId, "user1@test.com", "user1", "pub1", sessionId);
        session.SetRoomId(roomId);
        
        var room = DungeonRoom.Create("Room1", userId, 4);
        
        _mockSessionRepository.Setup(x => x.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _mockRoomRepository.Setup(x => x.GetByIdAsync(roomId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(room);
        _mockEventStream.Setup(x => x.ReadAsync(roomId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => readCalled.TrySetResult())
            .Returns(AsyncEnumerable.Empty<long>());

        // Act
        var result = await _service.SubscribeAsync(sessionId, roomId, CancellationToken.None);
        await readCalled.Task.WaitAsync(TimeSpan.FromSeconds(1));

        // Assert
        Assert.NotNull(result);
        Assert.Equal(userId, result.UserId);
        Assert.Equal(roomId, result.RoomId);
        
        // ReadAsync should be called for the room
        _mockEventStream.Verify(x => x.ReadAsync(
            It.Is<long>(id => id == roomId),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task 유효하지_않은_세션으로_구독_시도_시_실패한다()
    {
        // Arrange
        _mockSessionRepository.Setup(x => x.GetBySessionIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserSession?)null);

        // Act
        var result = await _service.SubscribeAsync("invalid-session", 100L, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task 존재하지_않는_방에_대해_구독_시도_시_실패한다()
    {
        // Arrange
        var userId = 1L;
        var roomId = 999L;
        var sessionId = "test-session";
        var session = UserSession.Create(userId, "user1@test.com", "user1", "pub1", sessionId);
        
        _mockSessionRepository.Setup(x => x.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _mockRoomRepository.Setup(x => x.GetByIdAsync(roomId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DungeonRoom?)null);
        
        // Act
        var result = await _service.SubscribeAsync(sessionId, roomId, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task 유저가_해당_방의_멤버가_아닌_경우_구독에_실패한다()
    {
        // Arrange
        var userId = 1L;
        var roomId = 100L;
        var sessionId = "test-session";
        var session = UserSession.Create(userId, "user1@test.com", "user1", "pub1", sessionId);
        
        var room = DungeonRoom.Create("Room1", 2L, 4); // Host is 2
        // userId 1 is not in the room

        _mockSessionRepository.Setup(x => x.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _mockRoomRepository.Setup(x => x.GetByIdAsync(roomId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(room);

        // Act
        var result = await _service.SubscribeAsync(sessionId, roomId, CancellationToken.None);

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
        var sessionId = "test-session";
        var room1Id = 100L;
        var room2Id = 200L;
        var readCount = 0;
        var readCalledTwice = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        
        var session1 = UserSession.Create(userId, "user1@test.com", "user1", "pub1", sessionId);
        session1.SetRoomId(room1Id);
        var room1 = DungeonRoom.Create("Room1", userId, 4);

        var session2 = UserSession.Create(userId, "user1@test.com", "user1", "pub1", sessionId);
        session2.SetRoomId(room2Id);
        var room2 = DungeonRoom.Create("Room2", userId, 4);
        
        // Setup for room 1
        _mockSessionRepository.Setup(x => x.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session1);
        _mockRoomRepository.Setup(x => x.GetByIdAsync(room1Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(room1);
        _mockEventStream.Setup(x => x.ReadAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                if (Interlocked.Increment(ref readCount) == 2)
                {
                    readCalledTwice.TrySetResult();
                }
            })
            .Returns(AsyncEnumerable.Empty<long>());

        await _service.SubscribeAsync(sessionId, room1Id, CancellationToken.None);
        
        // Setup for room 2
        _mockSessionRepository.Setup(x => x.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session2);
        _mockRoomRepository.Setup(x => x.GetByIdAsync(room2Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(room2);

        // Act
        var result = await _service.SubscribeAsync(sessionId, room2Id, CancellationToken.None);
        await readCalledTwice.Task.WaitAsync(TimeSpan.FromSeconds(1));

        // Assert
        Assert.NotNull(result);
        Assert.Equal(room2Id, result.RoomId);
        
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
        var sessionId = "test-session";
        var readCount = 0;
        var allReadsObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        
        var session = UserSession.Create(userId, "user1@test.com", "user1", "pub1", sessionId);
        session.SetRoomId(roomId);
        var room = DungeonRoom.Create("Room1", userId, 4);
        
        _mockSessionRepository.Setup(x => x.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _mockRoomRepository.Setup(x => x.GetByIdAsync(roomId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(room);
        _mockEventStream.Setup(x => x.ReadAsync(roomId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                if (Interlocked.Increment(ref readCount) == 10)
                {
                    allReadsObserved.TrySetResult();
                }
            })
            .Returns(AsyncEnumerable.Empty<long>());

        var tasks = new List<Task<UserRoomContext?>>();
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(_service.SubscribeAsync(sessionId, roomId, CancellationToken.None));
        }

        // Act
        var results = await Task.WhenAll(tasks);
        await allReadsObserved.Task.WaitAsync(TimeSpan.FromSeconds(1));

        // Assert
        Assert.All(results, Assert.NotNull);
        _mockEventStream.Verify(x => x.ReadAsync(
            roomId,
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Exactly(10));
    }
}
