using GameServer.Application.Common;
using GameServer.Application.Domains.Chat.Interfaces;
using GameServer.Application.Domains.DungeonLobby;
using GameServer.Application.Domains.DungeonLobby.Interfaces;
using GameServer.Application.Domains.Outbox;
using GameServer.Domain.Entities;
using GameServer.Domain.Entities.Outbox;
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
    private readonly FakeUserProfileRepository _userProfileRepository = new();
    private readonly Mock<IChatSubscriptionService> _mockChatSubscriptionService = new();
    private readonly Mock<IDungeonLobbySubscriptionService> _mockDungeonLobbySubscriptionService = new();
    private readonly Mock<IOutboxRepository> _mockOutboxRepository = new();
    private readonly DungeonLobbyService _service;

    public DungeonLobbyServiceTests()
    {
        _service = new DungeonLobbyService(
            _roomRepository,
            _mockDungeonLobbySubscriptionService.Object,
            _roomPlayerRepository,
            _mockOutboxRepository.Object,
            _sessionRepository,
            _mockChatSubscriptionService.Object,
            _userProfileRepository,
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
        _mockOutboxRepository.Verify(
            r => r.AddWithRoomUpdateAsync(
                It.Is<DungeonRoom>(rm => rm.RoomId == created.Value.RoomId && rm.Status == RoomStatus.Starting),
                It.Is<OutboxMessage>(m => m.Topic == OutboxTopics.GameStartRequested),
                It.IsAny<CancellationToken>()),
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

    [Fact]
    public async Task ValidateSubscription_성공()
    {
        var session = await _sessionRepository.CreateSessionAsync(1);
        var created = await _service.CreateDungeonRoomAsync(session!.SessionId, "Room", 4);

        var result = await _service.ValidateSubscriptionAsync(session.SessionId, created.Value!.RoomId);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value);
    }

    [Fact]
    public async Task ValidateSubscription_방에_없을_시_실패()
    {
        var hostSession = await _sessionRepository.CreateSessionAsync(1);
        var otherSession = await _sessionRepository.CreateSessionAsync(2);
        var created = await _service.CreateDungeonRoomAsync(hostSession!.SessionId, "Room", 4);

        var result = await _service.ValidateSubscriptionAsync(otherSession!.SessionId, created.Value!.RoomId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.NotInRoom, result.InternalErrorCode);
    }

    // ── CreateRoom 추가 케이스 ─────────────────────────────────────

    [Fact]
    public async Task CreateRoom_이미_방에_있는_경우_실패()
    {
        var session = await _sessionRepository.CreateSessionAsync(1);
        await _service.CreateDungeonRoomAsync(session!.SessionId, "First Room", 4);

        var result = await _service.CreateDungeonRoomAsync(session.SessionId, "Second Room", 4);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.AlreadyInRoom, result.InternalErrorCode);
    }

    // ── GetActiveDungeonRooms ──────────────────────────────────────

    [Fact]
    public async Task GetActiveDungeonRooms_방이_없으면_빈_목록_반환()
    {
        var result = await _service.GetActiveDungeonRoomsAsync();

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task GetActiveDungeonRooms_생성된_방_목록_반환()
    {
        var session1 = await _sessionRepository.CreateSessionAsync(1);
        var session2 = await _sessionRepository.CreateSessionAsync(2);
        await _service.CreateDungeonRoomAsync(session1!.SessionId, "Room A", 4);
        await _service.CreateDungeonRoomAsync(session2!.SessionId, "Room B", 4);

        var result = await _service.GetActiveDungeonRoomsAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count());
    }

    // ── GetDungeonRoom ─────────────────────────────────────────────

    [Fact]
    public async Task GetDungeonRoom_존재하는_방_조회_성공()
    {
        var session = await _sessionRepository.CreateSessionAsync(1);
        var created = await _service.CreateDungeonRoomAsync(session!.SessionId, "Room", 4);

        var result = await _service.GetDungeonRoomAsync(created.Value!.RoomId);

        Assert.True(result.IsSuccess);
        Assert.Equal(created.Value.RoomId, result.Value!.RoomId);
    }

    [Fact]
    public async Task GetDungeonRoom_없는_방_조회_실패()
    {
        var result = await _service.GetDungeonRoomAsync(999999L);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.RoomNotFound, result.InternalErrorCode);
    }

    // ── JoinRoom 추가 케이스 ───────────────────────────────────────

    [Fact]
    public async Task JoinRoom_없는_방에_입장_시_실패()
    {
        var session = await _sessionRepository.CreateSessionAsync(1);

        var result = await _service.JoinRoomAsync(session!.SessionId, 999999L);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.RoomNotFound, result.InternalErrorCode);
    }

    [Fact]
    public async Task JoinRoom_이미_방에_있는_경우_실패()
    {
        var hostSession = await _sessionRepository.CreateSessionAsync(1);
        var joinSession = await _sessionRepository.CreateSessionAsync(2);
        var created = await _service.CreateDungeonRoomAsync(hostSession!.SessionId, "Room", 4);
        await _service.JoinRoomAsync(joinSession!.SessionId, created.Value!.RoomId);

        // 같은 유저가 다시 입장 시도
        var result = await _service.JoinRoomAsync(joinSession.SessionId, created.Value.RoomId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.AlreadyInRoom, result.InternalErrorCode);
    }

    [Fact]
    public async Task JoinRoom_방이_가득_찬_경우_실패()
    {
        var hostSession   = await _sessionRepository.CreateSessionAsync(1);
        var player2Session = await _sessionRepository.CreateSessionAsync(2);
        var player3Session = await _sessionRepository.CreateSessionAsync(3);
        var created = await _service.CreateDungeonRoomAsync(hostSession!.SessionId, "Room", 2); // 최대 2명
        await _service.JoinRoomAsync(player2Session!.SessionId, created.Value!.RoomId);

        var result = await _service.JoinRoomAsync(player3Session!.SessionId, created.Value.RoomId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.JoinRoomFailed, result.InternalErrorCode);
    }

    [Fact]
    public async Task JoinRoom_Starting_상태의_방에는_입장할_수_없다()
    {
        var hostSession   = await _sessionRepository.CreateSessionAsync(1);
        var player2Session = await _sessionRepository.CreateSessionAsync(2);
        var player3Session = await _sessionRepository.CreateSessionAsync(3);
        var created = await _service.CreateDungeonRoomAsync(hostSession!.SessionId, "Room", 4);
        await _service.JoinRoomAsync(player2Session!.SessionId, created.Value!.RoomId);
        await _service.StartGameAsync(hostSession.SessionId, created.Value.RoomId, "trace");

        var result = await _service.JoinRoomAsync(player3Session!.SessionId, created.Value.RoomId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.JoinRoomFailed, result.InternalErrorCode);
    }

    // ── LeaveRoom 추가 케이스 ──────────────────────────────────────

    [Fact]
    public async Task LeaveRoom_방장이_퇴장하면_다음_플레이어가_방장이_된다()
    {
        var hostSession   = await _sessionRepository.CreateSessionAsync(1);
        var player2Session = await _sessionRepository.CreateSessionAsync(2);
        var created = await _service.CreateDungeonRoomAsync(hostSession!.SessionId, "Room", 4);
        await _service.JoinRoomAsync(player2Session!.SessionId, created.Value!.RoomId);

        var result = await _service.LeaveRoomAsync(hostSession.SessionId, created.Value.RoomId);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.HostUserId); // 2번 유저가 새 방장
    }

    [Fact]
    public async Task LeaveRoom_방에_없는_유저가_퇴장_시도하면_실패()
    {
        var hostSession  = await _sessionRepository.CreateSessionAsync(1);
        var otherSession = await _sessionRepository.CreateSessionAsync(2);
        var created = await _service.CreateDungeonRoomAsync(hostSession!.SessionId, "Room", 4);

        var result = await _service.LeaveRoomAsync(otherSession!.SessionId, created.Value!.RoomId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.NotInRoom, result.InternalErrorCode);
    }

    // ── UpdateRoomSettings 추가 케이스 ────────────────────────────

    [Fact]
    public async Task UpdateRoomSettings_없는_방_수정_시_실패()
    {
        var session = await _sessionRepository.CreateSessionAsync(1);

        var result = await _service.UpdateRoomSettingsAsync(session!.SessionId, 999999L, "New Name");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.RoomNotFound, result.InternalErrorCode);
    }

    // ── StartGame 추가 케이스 ──────────────────────────────────────

    [Fact]
    public async Task StartGame_플레이어_1명이어도_성공한다()
    {
        // 현재 정책: 최소 1명이면 게임 시작 가능 (단일 클라 개발 테스트 허용).
        var hostSession = await _sessionRepository.CreateSessionAsync(1);
        var created = await _service.CreateDungeonRoomAsync(hostSession!.SessionId, "Room", 4);
        // 방장만 있고 추가 입장 없음 → 1명

        var result = await _service.StartGameAsync(hostSession.SessionId, created.Value!.RoomId, "trace");

        Assert.True(result.IsSuccess);
        Assert.Equal(RoomStatus.Starting, result.Value!.Status);
    }

    // ── RemovePlayerFromRoom 케이스 ────────────────────────────────
    // SocketServer가 "플레이어 퇴장" 이벤트(PlayerLeftRoomMessage)를 Redis Stream으로 전달하면
    // RoomLifecycleConsumer가 RemovePlayerFromRoomAsync를 호출한다.

    [Fact]
    public async Task RemovePlayer_마지막_플레이어면_방_삭제_및_association_제거()
    {
        var session = await _sessionRepository.CreateSessionAsync(1);
        var created = await _service.CreateDungeonRoomAsync(session!.SessionId, "Room", 4);

        // 퇴장 전: 유저는 방에 연결돼 있다 (로그인 시 CurrentRoomId로 복원되는 근거).
        Assert.NotNull(await _roomPlayerRepository.GetByUserIdAsync(1));

        var result = await _service.RemovePlayerFromRoomAsync(created.Value!.RoomId, 1);

        Assert.True(result.IsSuccess);
        // 빈 방 → 삭제 + association 제거 → GetByUserIdAsync null → CurrentRoomId=0 → 복원 안 됨.
        Assert.False((await _service.GetDungeonRoomAsync(created.Value.RoomId)).IsSuccess);
        Assert.Null(await _roomPlayerRepository.GetByUserIdAsync(1));
    }

    [Fact]
    public async Task RemovePlayer_N인_부분퇴장이면_떠난사람만_제거하고_방은_유지()
    {
        var hostSession = await _sessionRepository.CreateSessionAsync(1);
        var guestSession = await _sessionRepository.CreateSessionAsync(2);
        var created = await _service.CreateDungeonRoomAsync(hostSession!.SessionId, "Room", 4);
        await _service.JoinRoomAsync(guestSession!.SessionId, created.Value!.RoomId);

        // 게스트(2)만 퇴장
        var result = await _service.RemovePlayerFromRoomAsync(created.Value.RoomId, 2);

        Assert.True(result.IsSuccess);
        // 떠난 사람(2)만 association 제거 → 재로그인 복원 안 됨.
        Assert.Null(await _roomPlayerRepository.GetByUserIdAsync(2));
        // 남은 사람(1)은 그대로 → 여전히 복원 가능. 방도 유지.
        Assert.NotNull(await _roomPlayerRepository.GetByUserIdAsync(1));
        Assert.True((await _service.GetDungeonRoomAsync(created.Value.RoomId)).IsSuccess);
    }

    [Fact]
    public async Task RemovePlayer_호스트가_나가면_다음_사람이_호스트가_된다()
    {
        var hostSession = await _sessionRepository.CreateSessionAsync(1);
        var guestSession = await _sessionRepository.CreateSessionAsync(2);
        var created = await _service.CreateDungeonRoomAsync(hostSession!.SessionId, "Room", 4);
        await _service.JoinRoomAsync(guestSession!.SessionId, created.Value!.RoomId);

        // 호스트(1) 퇴장
        var result = await _service.RemovePlayerFromRoomAsync(created.Value.RoomId, 1);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.HostUserId); // 2번이 새 호스트
    }

    [Fact]
    public async Task RemovePlayer_이미_없는_플레이어면_멱등하게_성공()
    {
        var session = await _sessionRepository.CreateSessionAsync(1);
        var created = await _service.CreateDungeonRoomAsync(session!.SessionId, "Room", 4);
        await _service.RemovePlayerFromRoomAsync(created.Value!.RoomId, 1); // 방 삭제됨

        // 중복 소비(at-least-once): 같은 메시지 재처리 — 방이 없어도 NotFound는 정상.
        var result = await _service.RemovePlayerFromRoomAsync(created.Value.RoomId, 1);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.RoomNotFound, result.InternalErrorCode);
    }

    [Fact]
    public async Task RemovePlayer_없는_방이면_RoomNotFound_반환한다()
    {
        var result = await _service.RemovePlayerFromRoomAsync(999999L, 1);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.RoomNotFound, result.InternalErrorCode);
    }

    [Fact]
    public async Task RemovePlayer_레포가_KeyNotFound를_던져도_멱등_RoomNotFound_반환한다()
    {
        // 실제 DungeonRoomRepository.GetByIdAsync는 없는 방에 KeyNotFoundException을 던진다(fake는 null).
        // at-least-once 중복 전달(이미 삭제된 방)이어도 INTERNAL_SERVER_ERROR가 아니라 멱등 처리돼야 한다.
        var throwingRoomRepo = new Mock<IDungeonRoomRepository>();
        throwingRoomRepo
            .Setup(r => r.GetByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("not found"));

        var service = new DungeonLobbyService(
            throwingRoomRepo.Object,
            _mockDungeonLobbySubscriptionService.Object,
            _roomPlayerRepository,
            _mockOutboxRepository.Object,
            _sessionRepository,
            _mockChatSubscriptionService.Object,
            _userProfileRepository,
            NullLogger<DungeonLobbyService>.Instance);

        var result = await service.RemovePlayerFromRoomAsync(999999L, 1);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.RoomNotFound, result.InternalErrorCode); // INTERNAL_SERVER_ERROR 아님
    }
}
