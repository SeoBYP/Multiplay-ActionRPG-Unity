using System.Text.Json;
using GameServer.Application.Common;
using GameServer.Application.Domains.Chat.Interfaces;
using GameServer.Application.Domains.DungeonLobby;
using GameServer.Application.Domains.DungeonLobby.Interfaces;
using GameServer.Application.Domains.Outbox;
using GameServer.Application.Domains.Progression;
using GameServer.Domain.Entities;
using GameServer.Domain.Entities.Outbox;
using GameServer.Tests.Infrastructure.Fakes.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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
    // 실제 ProgressionService(Fake 저장소) — StartGame 이 GetStatsAsync 로 레벨 스탯을 메시지에 채운다(Lv1 기본).
    private readonly ProgressionService _progressionService = new(new FakeProgressionRepository(), new Infrastructure.Fakes.Services.FakeEquipmentService());
    private readonly FakeRoomReadyStore _readyStore = new();
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
            _progressionService,
            _readyStore,
            new Infrastructure.Fakes.NoOpDistributedLock(),
            Options.Create(new DungeonRoomReaperOptions()),
            NullLogger<DungeonLobbyService>.Instance);
    }

    // ── 유령 방 정리(리퍼) ────────────────────────────────────────────────
    // 시스템에 정식 하트비트가 없어 `session:active` 만료 시각이 유일한 생존 근사 신호다.
    // 그래서 유예를 넉넉히 두고, "전원이 유예를 넘겨 조용할 때만" 정리한다.

    [Fact]
    public async Task 전원이_유예를_넘겨_조용하면_유령_방이_정리된다()
    {
        var session = await _sessionRepository.CreateSessionAsync(1);
        var room = await _service.CreateDungeonRoomAsync(session!.SessionId, "Ghost Room", 4);
        Assert.True(room.IsSuccess);

        _sessionRepository.SetActiveUntil(1, DateTime.UtcNow.AddHours(-3));

        var reaped = await _service.ReapRoomIfAbandonedAsync(room.Value!.RoomId);

        Assert.True(reaped.IsSuccess);
        Assert.True(reaped.Value);
        Assert.Empty(await _roomPlayerRepository.GetPlayersByRoomIdAsync(room.Value.RoomId));
    }

    [Fact]
    public async Task 한_명이라도_최근_활동이_있으면_방은_유지된다()
    {
        var host = await _sessionRepository.CreateSessionAsync(1);
        var room = await _service.CreateDungeonRoomAsync(host!.SessionId, "Live Room", 4);
        var guest = await _sessionRepository.CreateSessionAsync(2);
        await _service.JoinRoomAsync(guest!.SessionId, room.Value!.RoomId);

        _sessionRepository.SetActiveUntil(1, DateTime.UtcNow.AddHours(-3));   // 호스트는 조용
        _sessionRepository.SetActiveUntil(2, DateTime.UtcNow.AddMinutes(5));  // 게스트는 살아 있음

        var reaped = await _service.ReapRoomIfAbandonedAsync(room.Value.RoomId);

        Assert.True(reaped.IsSuccess);
        Assert.False(reaped.Value);
        Assert.Equal(2, (await _roomPlayerRepository.GetPlayersByRoomIdAsync(room.Value.RoomId)).Count);
    }

    [Fact]
    public async Task 세션_신호가_아예_없는_방도_정리된다()
    {
        var session = await _sessionRepository.CreateSessionAsync(1);
        var room = await _service.CreateDungeonRoomAsync(session!.SessionId, "No Signal Room", 4);
        Assert.True(room.IsSuccess);

        _sessionRepository.SetActiveUntil(1, null);
        await _sessionRepository.RemoveSessionAsync(session.SessionId);

        var reaped = await _service.ReapRoomIfAbandonedAsync(room.Value!.RoomId);

        Assert.True(reaped.IsSuccess);
        Assert.True(reaped.Value);
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
    public async Task CreateRoom_mapId를_비우면_기본_맵으로_방에_영속된다()
    {
        var session = await _sessionRepository.CreateSessionAsync(1);

        var result = await _service.CreateDungeonRoomAsync(session!.SessionId, "Room", 4, mapId: "");

        Assert.True(result.IsSuccess);
        Assert.Equal(Shared.Infrastructure.Spawn.MapIds.Default, result.Value!.MapId);
    }

    [Fact]
    public async Task CreateRoom_지정한_mapId가_방에_영속된다()
    {
        var session = await _sessionRepository.CreateSessionAsync(1);

        var result = await _service.CreateDungeonRoomAsync(session!.SessionId, "Room", 4, mapId: "dungeon_01");

        Assert.True(result.IsSuccess);
        Assert.Equal("dungeon_01", result.Value!.MapId);
    }

    [Fact]
    public async Task CreateRoom_알수없는_mapId면_거부된다()
    {
        var session = await _sessionRepository.CreateSessionAsync(1);

        var result = await _service.CreateDungeonRoomAsync(session!.SessionId, "Room", 4, mapId: "does_not_exist");

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
        await _service.SetReadyAsync(user2Session.SessionId, created.Value.RoomId, true);

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
        var result = await _service.GetActiveDungeonRoomsAsync(offset: 0, limit: 20);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Rooms);
        Assert.Equal(0, result.Value!.TotalCount);
    }

    [Fact]
    public async Task GetActiveDungeonRooms_생성된_방_목록_반환()
    {
        var session1 = await _sessionRepository.CreateSessionAsync(1);
        var session2 = await _sessionRepository.CreateSessionAsync(2);
        await _service.CreateDungeonRoomAsync(session1!.SessionId, "Room A", 4);
        await _service.CreateDungeonRoomAsync(session2!.SessionId, "Room B", 4);

        var result = await _service.GetActiveDungeonRoomsAsync(offset: 0, limit: 20);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Rooms.Count);
        Assert.Equal(2, result.Value!.TotalCount);
    }

    // ── GetActiveDungeonRooms 페이징 (9.6) ─────────────────────────

    [Fact]
    public async Task GetActiveDungeonRooms_요청한_페이지_크기만큼만_반환하고_전체수는_따로_준다()
    {
        await CreateRoomsAsync(5);

        var result = await _service.GetActiveDungeonRoomsAsync(offset: 0, limit: 2);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Rooms.Count);
        Assert.Equal(5, result.Value!.TotalCount);
    }

    [Fact]
    public async Task GetActiveDungeonRooms_페이지들이_겹치지_않고_전체를_덮는다()
    {
        // 안정 정렬이 없으면(Redis room:active 는 Set=무순서) 여기서 중복·누락이 난다.
        await CreateRoomsAsync(5);

        var page1 = await _service.GetActiveDungeonRoomsAsync(offset: 0, limit: 2);
        var page2 = await _service.GetActiveDungeonRoomsAsync(offset: 2, limit: 2);
        var page3 = await _service.GetActiveDungeonRoomsAsync(offset: 4, limit: 2);

        var ids = page1.Value!.Rooms.Concat(page2.Value!.Rooms).Concat(page3.Value!.Rooms)
            .Select(r => r.RoomId).ToList();

        Assert.Equal(5, ids.Count);
        Assert.Equal(5, ids.Distinct().Count());
    }

    [Fact]
    public async Task GetActiveDungeonRooms_최신_방이_먼저_온다()
    {
        var created = await CreateRoomsAsync(3);

        var result = await _service.GetActiveDungeonRoomsAsync(offset: 0, limit: 20);

        var expected = created.OrderByDescending(id => id).ToList();
        Assert.Equal(expected, result.Value!.Rooms.Select(r => r.RoomId).ToList());
    }

    [Fact]
    public async Task GetActiveDungeonRooms_크기를_안보내면_기본값이_적용된다()
    {
        await CreateRoomsAsync(3);

        var result = await _service.GetActiveDungeonRoomsAsync(offset: 0, limit: 0);

        Assert.Equal(3, result.Value!.Rooms.Count); // 3 < DefaultPageSize(20)
    }

    [Fact]
    public async Task GetActiveDungeonRooms_과도한_요청_크기는_상한으로_잘린다()
    {
        await CreateRoomsAsync(DungeonLobbyPaging.MaxPageSize + 5);

        var result = await _service.GetActiveDungeonRoomsAsync(offset: 0, limit: int.MaxValue);

        Assert.Equal(DungeonLobbyPaging.MaxPageSize, result.Value!.Rooms.Count);
        Assert.Equal(DungeonLobbyPaging.MaxPageSize + 5, result.Value!.TotalCount);
    }

    [Fact]
    public async Task GetActiveDungeonRooms_음수_오프셋은_처음부터로_취급한다()
    {
        await CreateRoomsAsync(3);

        var result = await _service.GetActiveDungeonRoomsAsync(offset: -10, limit: 20);

        Assert.Equal(3, result.Value!.Rooms.Count);
    }

    [Fact]
    public async Task GetActiveDungeonRooms_범위를_넘은_오프셋은_빈_페이지다()
    {
        await CreateRoomsAsync(3);

        var result = await _service.GetActiveDungeonRoomsAsync(offset: 100, limit: 20);

        Assert.Empty(result.Value!.Rooms);
        Assert.Equal(3, result.Value!.TotalCount); // 전체수는 여전히 알려준다
    }

    /// <summary>방 N 개 생성(방장은 각각 다른 유저 — 한 유저는 한 방만 가질 수 있다). 생성된 RoomId 목록 반환.</summary>
    private async Task<List<long>> CreateRoomsAsync(int count)
    {
        var ids = new List<long>(count);
        for (int i = 0; i < count; i++)
        {
            var session = await _sessionRepository.CreateSessionAsync(1000 + i);
            var created = await _service.CreateDungeonRoomAsync(session!.SessionId, $"Room {i}", 4);
            ids.Add(created.Value!.RoomId);
        }
        return ids;
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
        await _service.SetReadyAsync(player2Session.SessionId, created.Value.RoomId, true);
        await _service.StartGameAsync(hostSession.SessionId, created.Value.RoomId, "trace");

        var result = await _service.JoinRoomAsync(player3Session!.SessionId, created.Value.RoomId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.JoinRoomFailed, result.InternalErrorCode);
    }

    // ── 준비(Ready) 상태 ──────────────────────────────────────────

    [Fact]
    public async Task SetReady_비호스트가_준비하면_준비목록에_담긴다()
    {
        var hostSession = await _sessionRepository.CreateSessionAsync(1);
        var guestSession = await _sessionRepository.CreateSessionAsync(2);
        var created = await _service.CreateDungeonRoomAsync(hostSession!.SessionId, "Room", 4);
        await _service.JoinRoomAsync(guestSession!.SessionId, created.Value!.RoomId);

        var result = await _service.SetReadyAsync(guestSession.SessionId, created.Value.RoomId, true);

        Assert.True(result.IsSuccess);
        Assert.Contains(2L, await _readyStore.GetReadyUserIdsAsync(created.Value.RoomId));
    }

    [Fact]
    public async Task SetReady_준비를_해제하면_준비목록에서_빠진다()
    {
        var hostSession = await _sessionRepository.CreateSessionAsync(1);
        var guestSession = await _sessionRepository.CreateSessionAsync(2);
        var created = await _service.CreateDungeonRoomAsync(hostSession!.SessionId, "Room", 4);
        await _service.JoinRoomAsync(guestSession!.SessionId, created.Value!.RoomId);
        await _service.SetReadyAsync(guestSession.SessionId, created.Value.RoomId, true);

        var result = await _service.SetReadyAsync(guestSession.SessionId, created.Value.RoomId, false);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(2L, await _readyStore.GetReadyUserIdsAsync(created.Value.RoomId));
    }

    [Fact]
    public async Task SetReady_호스트는_준비_개념이_없어_거부된다()
    {
        var hostSession = await _sessionRepository.CreateSessionAsync(1);
        var created = await _service.CreateDungeonRoomAsync(hostSession!.SessionId, "Room", 4);

        var result = await _service.SetReadyAsync(hostSession.SessionId, created.Value!.RoomId, true);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.InvalidRequest, result.InternalErrorCode);
    }

    [Fact]
    public async Task SetReady_방에_없는_유저는_거부된다()
    {
        var hostSession = await _sessionRepository.CreateSessionAsync(1);
        var outsiderSession = await _sessionRepository.CreateSessionAsync(2);
        var created = await _service.CreateDungeonRoomAsync(hostSession!.SessionId, "Room", 4);

        var result = await _service.SetReadyAsync(outsiderSession!.SessionId, created.Value!.RoomId, true);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.NotInRoom, result.InternalErrorCode);
    }

    [Fact]
    public async Task SetReady_대기중이_아닌_방에서는_거부된다()
    {
        var hostSession = await _sessionRepository.CreateSessionAsync(1);
        var guestSession = await _sessionRepository.CreateSessionAsync(2);
        var created = await _service.CreateDungeonRoomAsync(hostSession!.SessionId, "Room", 4);
        await _service.JoinRoomAsync(guestSession!.SessionId, created.Value!.RoomId);
        await _service.SetReadyAsync(guestSession.SessionId, created.Value.RoomId, true);
        await _service.StartGameAsync(hostSession.SessionId, created.Value.RoomId, "trace");

        var result = await _service.SetReadyAsync(guestSession.SessionId, created.Value.RoomId, false);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.RoomNotWaiting, result.InternalErrorCode);
    }

    [Fact]
    public async Task StartGame_비호스트가_한명이라도_미준비면_거부된다()
    {
        var hostSession = await _sessionRepository.CreateSessionAsync(1);
        var readySession = await _sessionRepository.CreateSessionAsync(2);
        var lazySession = await _sessionRepository.CreateSessionAsync(3);
        var created = await _service.CreateDungeonRoomAsync(hostSession!.SessionId, "Room", 4);
        await _service.JoinRoomAsync(readySession!.SessionId, created.Value!.RoomId);
        await _service.JoinRoomAsync(lazySession!.SessionId, created.Value.RoomId);
        await _service.SetReadyAsync(readySession.SessionId, created.Value.RoomId, true);

        var result = await _service.StartGameAsync(hostSession.SessionId, created.Value.RoomId, "trace");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.NotAllPlayersReady, result.InternalErrorCode);
        Assert.Equal(RoomStatus.Waiting, (await _roomRepository.GetByIdAsync(created.Value.RoomId))!.Status);
    }

    [Fact]
    public async Task StartGame_호스트_혼자면_준비_대상이_없어_시작된다()
    {
        var hostSession = await _sessionRepository.CreateSessionAsync(1);
        var created = await _service.CreateDungeonRoomAsync(hostSession!.SessionId, "Room", 4);

        var result = await _service.StartGameAsync(hostSession.SessionId, created.Value!.RoomId, "trace");

        Assert.True(result.IsSuccess);
        Assert.Equal(RoomStatus.Starting, result.Value!.Status);
    }

    [Fact]
    public async Task LeaveRoom_퇴장하면_준비_상태도_함께_지워진다()
    {
        var hostSession = await _sessionRepository.CreateSessionAsync(1);
        var guestSession = await _sessionRepository.CreateSessionAsync(2);
        var created = await _service.CreateDungeonRoomAsync(hostSession!.SessionId, "Room", 4);
        await _service.JoinRoomAsync(guestSession!.SessionId, created.Value!.RoomId);
        await _service.SetReadyAsync(guestSession.SessionId, created.Value.RoomId, true);

        await _service.LeaveRoomAsync(guestSession.SessionId, created.Value.RoomId);

        Assert.DoesNotContain(2L, await _readyStore.GetReadyUserIdsAsync(created.Value.RoomId));
    }

    [Fact]
    public async Task RemovePlayerFromRoom_퇴장하면_준비_상태도_함께_지워진다()
    {
        var hostSession = await _sessionRepository.CreateSessionAsync(1);
        var guestSession = await _sessionRepository.CreateSessionAsync(2);
        var created = await _service.CreateDungeonRoomAsync(hostSession!.SessionId, "Room", 4);
        await _service.JoinRoomAsync(guestSession!.SessionId, created.Value!.RoomId);
        await _service.SetReadyAsync(guestSession.SessionId, created.Value.RoomId, true);

        await _service.RemovePlayerFromRoomAsync(created.Value.RoomId, 2);

        Assert.DoesNotContain(2L, await _readyStore.GetReadyUserIdsAsync(created.Value.RoomId));
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
            _progressionService,
            _readyStore,
            new Infrastructure.Fakes.NoOpDistributedLock(),
            Options.Create(new DungeonRoomReaperOptions()),
            NullLogger<DungeonLobbyService>.Instance);

        var result = await service.RemovePlayerFromRoomAsync(999999L, 1);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.RoomNotFound, result.InternalErrorCode); // INTERNAL_SERVER_ERROR 아님
    }

    // ── StartGame 캐시 무효화 회귀 테스트 ─────────────────────────────
    // 버그: AddWithRoomUpdateAsync가 DB는 Starting으로 업데이트하지만 Redis 캐시를 무효화하지 않아
    // GameSessionReadyConsumer가 stale Waiting 상태를 읽고 MarkGameSessionReady()를 스킵,
    // 결과적으로 GameSessionEvent가 전송되지 않아 씬 전환이 안 됨.
    // 수정: StartGameAsync 이후 InvalidateCacheAsync를 반드시 호출해야 한다.

    [Fact]
    public async Task StartGame_성공_시_캐시_무효화가_반드시_호출된다()
    {
        // Arrange
        var hostSession = await _sessionRepository.CreateSessionAsync(1);
        var created = await _service.CreateDungeonRoomAsync(hostSession!.SessionId, "Room", 4);
        var roomId = created.Value!.RoomId;

        // Act
        var result = await _service.StartGameAsync(hostSession.SessionId, roomId, "trace");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, _roomRepository.InvalidateCacheCallCount); // 캐시 무효화 1회 호출 필수
    }

    [Fact]
    public async Task StartGame_이미_Starting_상태에서_재시도_시_캐시_무효화가_호출된다()
    {
        // Arrange: 방을 Starting 상태로 만들기
        var hostSession = await _sessionRepository.CreateSessionAsync(1);
        var created = await _service.CreateDungeonRoomAsync(hostSession!.SessionId, "Room", 4);
        var roomId = created.Value!.RoomId;
        await _service.StartGameAsync(hostSession.SessionId, roomId, "trace-first");
        var callCountAfterFirst = _roomRepository.InvalidateCacheCallCount;

        // Act: 이미 Starting인 방에 StartGame 재호출 (OutboxMessage 재발행 경로)
        var result = await _service.StartGameAsync(hostSession.SessionId, roomId, "trace-retry");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(_roomRepository.InvalidateCacheCallCount > callCountAfterFirst); // 재시도에서도 캐시 무효화
    }

    // ── 스탯 전파(2.4): StartGame 메시지에 참가자의 레벨 스탯이 실린다 ──
    // GameServer 가 progression+레벨테이블로 합산한 스탯을 GameStartRequestedMessage 에 적재해야
    // SocketServer 가 DB 접근 없이 그 값으로 전투한다(authority-model §4c).

    [Fact]
    public async Task StartGame_메시지에_참가자의_레벨업된_스탯이_실린다()
    {
        var hostSession = await _sessionRepository.CreateSessionAsync(1);
        var created = await _service.CreateDungeonRoomAsync(hostSession!.SessionId, "Room", 4);
        var roomId = created.Value!.RoomId;

        // 호스트(userId=1)를 Lv2 로 만든다(Lv1 임계 100 초과) → 스탯이 Lv2 테이블값으로 올라야 함.
        await _progressionService.AddExpAsync(1, 120);

        OutboxMessage? captured = null;
        _mockOutboxRepository
            .Setup(r => r.AddWithRoomUpdateAsync(It.IsAny<DungeonRoom>(), It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()))
            .Callback<DungeonRoom, OutboxMessage, CancellationToken>((_, m, _) => captured = m)
            .Returns(Task.CompletedTask);

        var result = await _service.StartGameAsync(hostSession.SessionId, roomId, "trace");

        Assert.True(result.IsSuccess);
        Assert.NotNull(captured);
        var message = JsonSerializer.Deserialize<GameStartRequestedMessage>(captured!.Payload)!;
        var host = message.PlayerInfos.Single(p => p.UserId == 1);
        Assert.Equal(120, host.MaxHealth);  // Lv2: 100 + 1*20
        Assert.Equal(13, host.AttackPower); // Lv2: 10 + 1*3
        Assert.Equal(7, host.Defense);      // Lv2: 5  + 1*2
    }

    // ── 던전 메타(4.3): 방에 영속된 MapId 가 게임 시작 메시지에 실린다 ──
    // 명시 mapId 없이 StartGame 하면 방의 MapId(생성 시 결정)가 진실의 원천으로 쓰여야 한다.

    [Fact]
    public async Task StartGame_메시지의_MapId는_방에_영속된_MapId를_따른다()
    {
        var hostSession = await _sessionRepository.CreateSessionAsync(1);
        var created = await _service.CreateDungeonRoomAsync(hostSession!.SessionId, "Room", 4, mapId: "dungeon_01");
        var roomId = created.Value!.RoomId;

        OutboxMessage? captured = null;
        _mockOutboxRepository
            .Setup(r => r.AddWithRoomUpdateAsync(It.IsAny<DungeonRoom>(), It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()))
            .Callback<DungeonRoom, OutboxMessage, CancellationToken>((_, m, _) => captured = m)
            .Returns(Task.CompletedTask);

        // mapId 인자를 비워도 방의 MapId 가 메시지에 실려야 한다.
        var result = await _service.StartGameAsync(hostSession.SessionId, roomId, "trace");

        Assert.True(result.IsSuccess);
        var message = JsonSerializer.Deserialize<GameStartRequestedMessage>(captured!.Payload)!;
        Assert.Equal("dungeon_01", message.MapId);
    }
}
