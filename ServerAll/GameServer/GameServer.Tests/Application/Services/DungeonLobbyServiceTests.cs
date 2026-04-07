using GameServer.Application.Common;
using GameServer.Application.Domains.Chat.Interfaces;
using GameServer.Application.Domains.DungeonLobby;
using GameServer.Application.Domains.DungeonLobby.Interfaces;
using GameServer.Domain.Entities;
using GameServer.Tests.Infrastructure.Fakes.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shared.Infrastructure.MessageQueue;
using Shared.Infrastructure.Messages;

namespace GameServer.Tests.Application.Services;

public class DungeonLobbyServiceTests
{
    private readonly FakeUserSessionRepository _sessionRepository = new();
    private readonly FakeDungeonRoomRepository _roomRepository = new();
    private readonly FakeDungeonRoomPlayerRepository _roomPlayerRepository = new();
    private readonly Mock<IChatSubscriptionService> _mockChatSubscriptionService = new();
    private readonly Mock<IDungeonLobbySubscriptionService> _mockDungeonLobbySubscriptionService = new();
    private readonly Mock<IMessageQueue<GameStartRequestedMessage>> _mockGameStartRequestedQueue = new();
    private readonly DungeonLobbyService _service;

    public DungeonLobbyServiceTests()
    {
        _service = new DungeonLobbyService(
            _roomRepository,
            _mockDungeonLobbySubscriptionService.Object,
            _roomPlayerRepository,
            _mockGameStartRequestedQueue.Object,
            _sessionRepository,
            _mockChatSubscriptionService.Object,
            NullLogger<DungeonLobbyService>.Instance);
    }

    [Fact]
    public async Task CreateRoom_방_생성_성공_및_방장_자동_입장()
    {
        var session = await _sessionRepository.CreateSessionAsync(1);

        var result = await _service.CreateDungeonRoomAsync(session!.SessionId, "Test Room", 4);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.HostUserId);

        var players = await _roomPlayerRepository.GetPlayersByRoomIdAsync(result.Value.RoomId);
        Assert.Single(players);
        Assert.Equal(1, players[0].UserId);
    }

    [Fact]
    public async Task CreateRoom_유효하지_않은_세션_실패()
    {
        var result = await _service.CreateDungeonRoomAsync("invalid-session", "Test Room", 4);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.InvalidRequest, result.InternalErrorCode);
    }

    [Fact]
    public async Task JoinRoom_입장_성공()
    {
        var hostSession = await _sessionRepository.CreateSessionAsync(1);
        var joinSession = await _sessionRepository.CreateSessionAsync(2);
        var created = await _service.CreateDungeonRoomAsync(hostSession!.SessionId, "Room", 4);

        var result = await _service.JoinRoomAsync(joinSession!.SessionId, created.Value!.RoomId);

        Assert.True(result.IsSuccess);
        var players = await _roomPlayerRepository.GetPlayersByRoomIdAsync(created.Value.RoomId);
        Assert.Contains(players, player => player.UserId == 2);
        _mockChatSubscriptionService.Verify(s => s.UpdateRoomSubscriptionAsync(joinSession.SessionId, created.Value.RoomId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LeaveRoom_마지막_플레이어_퇴장_시_방_삭제()
    {
        var session = await _sessionRepository.CreateSessionAsync(1);
        var created = await _service.CreateDungeonRoomAsync(session!.SessionId, "Room", 4);

        var result = await _service.LeaveRoomAsync(session.SessionId, created.Value!.RoomId);

        Assert.True(result.IsSuccess);
        Assert.Equal(RoomStatus.Closed, result.Value!.Status);
        Assert.False((await _service.GetDungeonRoomAsync(created.Value.RoomId)).IsSuccess);
    }

    [Fact]
    public async Task UpdateRoomSettings_방장이_설정_변경_가능()
    {
        var session = await _sessionRepository.CreateSessionAsync(1);
        var created = await _service.CreateDungeonRoomAsync(session!.SessionId, "Old Name", 4);

        var result = await _service.UpdateRoomSettingsAsync(session.SessionId, created.Value!.RoomId, "New Name", 3);

        Assert.True(result.IsSuccess);
        Assert.Equal("New Name", result.Value!.RoomName);
        Assert.Equal(3, result.Value.MaxPlayers);
    }

    [Fact]
    public async Task StartGame_방장이_게임_시작_및_메시지_큐_등록()
    {
        var hostSession = await _sessionRepository.CreateSessionAsync(1);
        var user2Session = await _sessionRepository.CreateSessionAsync(2);
        var created = await _service.CreateDungeonRoomAsync(hostSession!.SessionId, "Room", 4);
        await _service.JoinRoomAsync(user2Session!.SessionId, created.Value!.RoomId);

        var result = await _service.StartGameAsync(hostSession.SessionId, created.Value.RoomId, "trace-test");

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
    public async Task StartGame_방장이_아닌_경우_실패()
    {
        var hostSession = await _sessionRepository.CreateSessionAsync(1);
        var otherSession = await _sessionRepository.CreateSessionAsync(2);
        var created = await _service.CreateDungeonRoomAsync(hostSession!.SessionId, "Room", 4);
        await _service.JoinRoomAsync(otherSession!.SessionId, created.Value!.RoomId);

        var result = await _service.StartGameAsync(otherSession.SessionId, created.Value.RoomId, "trace-test");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.NotRoomHost, result.InternalErrorCode);
    }
}
