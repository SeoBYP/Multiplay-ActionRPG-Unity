using System.Collections.Concurrent;
using GameServer.Application.Common;
using GameServer.Application.Domains.Chat.Interfaces;
using GameServer.Application.Domains.DungeonLobby;
using GameServer.Application.Domains.DungeonLobby.Interfaces;
using GameServer.Application.Domains.User.Interfaces;
using GameServer.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shared.Infrastructure.MessageQueue;
using Shared.Infrastructure.Messages;

namespace GameServer.Tests.Application.Services;

public class DungeonLobbyServiceTests
{
    private readonly Mock<IUserSessionRepository> _mockSessionRepository = new();
    private readonly Mock<IDungeonRoomRepository> _mockRoomRepository = new();
    private readonly Mock<IChatSubscriptionService> _mockChatSubscriptionService = new();
    private readonly Mock<IDungeonLobbySubscriptionService> _mockDungeonLobbySubscriptionService = new();
    private readonly Mock<IMessageQueue<GameStartRequestedMessage>> _mockGameStartRequestedQueue = new();
    private readonly DungeonLobbyService _service;

    private readonly ConcurrentDictionary<long, DungeonRoom> _rooms = new();
    private readonly ConcurrentDictionary<string, UserSession> _sessions = new();
    private readonly ConcurrentDictionary<long, long> _userRoomMapping = new();
    private long _nextRoomId = 1;

    public DungeonLobbyServiceTests()
    {
        SetupMocks();

        _service = new DungeonLobbyService(
            _mockRoomRepository.Object,
            _mockDungeonLobbySubscriptionService.Object,
            _mockGameStartRequestedQueue.Object,
            _mockSessionRepository.Object,
            _mockChatSubscriptionService.Object,
            NullLogger<DungeonLobbyService>.Instance);
    }

    private void SetupMocks()
    {
        _mockRoomRepository.Setup(r => r.CreateAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long hostId, string roomName, int maxPlayers, CancellationToken _) =>
            {
                var room = DungeonRoom.Create(roomName, hostId, maxPlayers);
                var roomId = Interlocked.Increment(ref _nextRoomId);
                room.SetRoomId(roomId);
                _rooms[roomId] = room;
                _userRoomMapping[hostId] = roomId;
                return room;
            });

        _mockRoomRepository.Setup(r => r.GetByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long roomId, CancellationToken _) => _rooms.TryGetValue(roomId, out var room) ? room.Clone() : null);

        _mockRoomRepository.Setup(r => r.GetByUserIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long userId, CancellationToken _) =>
                _userRoomMapping.TryGetValue(userId, out var roomId) && _rooms.TryGetValue(roomId, out var room) ? room.Clone() : null);

        _mockRoomRepository.Setup(r => r.GetAllActiveRoomsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _rooms.Values.Where(r => r.Status != RoomStatus.Closed).ToList());

        _mockRoomRepository.Setup(r => r.UpdateAsync(It.IsAny<DungeonRoom>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DungeonRoom room, CancellationToken _) =>
            {
                if (!_rooms.ContainsKey(room.RoomId))
                    return false;

                var oldMappings = _userRoomMapping.Where(x => x.Value == room.RoomId).Select(x => x.Key).ToList();
                foreach (var userId in oldMappings)
                    _userRoomMapping.TryRemove(userId, out _);
                foreach (var userId in room.CurrentPlayers)
                    _userRoomMapping[userId] = room.RoomId;

                _rooms[room.RoomId] = room.Clone();
                return true;
            });

        _mockRoomRepository.Setup(r => r.DeleteAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long roomId, CancellationToken _) =>
            {
                if (!_rooms.TryRemove(roomId, out var room))
                    return false;

                foreach (var userId in room.CurrentPlayers)
                    _userRoomMapping.TryRemove(userId, out _);

                return true;
            });

        _mockRoomRepository.Setup(r => r.TryJoinRoomAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long userId, long roomId, CancellationToken _) =>
            {
                lock (_rooms)
                {
                    if (!_rooms.TryGetValue(roomId, out var room))
                        return JoinRoomAtomicResult.RoomNotFound;
                    if (room.Status != RoomStatus.Waiting)
                        return JoinRoomAtomicResult.InvalidStatus;
                    if (_userRoomMapping.TryGetValue(userId, out var joinedRoomId) && joinedRoomId != roomId)
                        return JoinRoomAtomicResult.AlreadyInOtherRoom;
                    if (room.IsExist(userId))
                        return JoinRoomAtomicResult.AlreadyInThisRoom;
                    if (room.IsFull)
                        return JoinRoomAtomicResult.RoomFull;

                    room.Join(userId);
                    _userRoomMapping[userId] = roomId;
                    return JoinRoomAtomicResult.Success;
                }
            });

        _mockSessionRepository.Setup(s => s.GetBySessionIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string sessionId, CancellationToken _) => _sessions.TryGetValue(sessionId, out var session) ? session : null);
    }

    [Fact]
    public async Task CreateRoom_SucceedsAndAutoJoinsHost()
    {
        var sessionId = await CreateTestSession(1, "user1");

        var result = await _service.CreateDungeonRoomAsync(sessionId, "Test Room", 4);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.HostUserId);
        Assert.Single(result.Value.CurrentPlayers);
    }

    [Fact]
    public async Task CreateRoom_InvalidSession_Fails()
    {
        var result = await _service.CreateDungeonRoomAsync("invalid-session", "Test Room", 4);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.InvalidRequest, result.InternalErrorCode);
    }

    [Fact]
    public async Task JoinRoom_Succeeds()
    {
        var hostSession = await CreateTestSession(1, "host");
        var joinSession = await CreateTestSession(2, "joiner");
        var created = await _service.CreateDungeonRoomAsync(hostSession, "Room", 4);

        var result = await _service.JoinRoomAsync(joinSession, created.Value!.RoomId);

        Assert.True(result.IsSuccess);
        Assert.Contains(2, result.Value!.CurrentPlayers);
        _mockChatSubscriptionService.Verify(s => s.SwitchRoomAsync(joinSession, created.Value.RoomId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LeaveRoom_LastPlayer_DeletesRoom()
    {
        var sessionId = await CreateTestSession(1, "user1");
        var created = await _service.CreateDungeonRoomAsync(sessionId, "Room", 4);

        var result = await _service.LeaveRoomAsync(sessionId, created.Value!.RoomId);

        Assert.True(result.IsSuccess);
        Assert.Equal(RoomStatus.Closed, result.Value!.Status);
        Assert.False((await _service.GetDungeonRoomAsync(created.Value.RoomId)).IsSuccess);
    }

    [Fact]
    public async Task UpdateRoomSettings_HostCanUpdate()
    {
        var sessionId = await CreateTestSession(1, "user1");
        var created = await _service.CreateDungeonRoomAsync(sessionId, "Old Name", 4);

        var result = await _service.UpdateRoomSettingsAsync(sessionId, created.Value!.RoomId, "New Name", 3);

        Assert.True(result.IsSuccess);
        Assert.Equal("New Name", result.Value!.RoomName);
        Assert.Equal(3, result.Value.MaxPlayers);
    }

    [Fact]
    public async Task StartGame_HostStartsAndEnqueuesStartRequest()
    {
        var hostSession = await CreateTestSession(1, "host");
        var user2Session = await CreateTestSession(2, "user2");
        var created = await _service.CreateDungeonRoomAsync(hostSession, "Room", 4);
        await _service.JoinRoomAsync(user2Session, created.Value!.RoomId);

        var result = await _service.StartGameAsync(hostSession, created.Value.RoomId, "trace-test");

        Assert.True(result.IsSuccess);
        Assert.Equal(RoomStatus.Starting, result.Value!.Status);
        _mockGameStartRequestedQueue.Verify(
            q => q.EnqueueAsync(It.Is<GameStartRequestedMessage>(m =>
                m.RoomId == created.Value.RoomId &&
                m.TraceId == "trace-test" &&
                m.PlayerIds.Count == 2)),
            Times.Once);
    }

    [Fact]
    public async Task StartGame_NotHost_Fails()
    {
        var hostSession = await CreateTestSession(1, "host");
        var otherSession = await CreateTestSession(2, "other");
        var created = await _service.CreateDungeonRoomAsync(hostSession, "Room", 4);
        await _service.JoinRoomAsync(otherSession, created.Value!.RoomId);

        var result = await _service.StartGameAsync(otherSession, created.Value.RoomId, "trace-test");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.NotRoomHost, result.InternalErrorCode);
    }

    private Task<string> CreateTestSession(long userId, string userName)
    {
        var email = $"{userName}@example.com";
        var publicId = $"PUB{userId:D8}";
        var sessionId = Guid.NewGuid().ToString();
        _sessions[sessionId] = UserSession.Create(userId, email, userName, publicId, sessionId);
        return Task.FromResult(sessionId);
    }
}
