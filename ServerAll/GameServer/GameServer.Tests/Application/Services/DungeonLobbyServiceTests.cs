using Moq;
using GameServer.Application.Common;
using GameServer.Application.Domains.Chat.Interfaces;
using GameServer.Application.Domains.DungeonLobby;
using GameServer.Application.Domains.DungeonLobby.Interfaces;
using GameServer.Application.Domains.User.Interfaces;
using GameServer.Domain.Entities;
using System.Collections.Concurrent;

namespace GameServer.Tests.Application.Services;

public class DungeonLobbyServiceTests
{
    private readonly Mock<IUserSessionRepository> _mockSessionRepository;
    private readonly Mock<IDungeonRoomRepository> _mockRoomRepository;
    private readonly Mock<IChatSubscriptionService> _mockChatSubscriptionService;
    private readonly Mock<IDungeonLobbySubscriptionService> _mockDungeonLobbySubscriptionService;
    private readonly DungeonLobbyService _service;

    // 테스트용 인메모리 저장소
    private readonly ConcurrentDictionary<long, DungeonRoom> _rooms = new();
    private readonly ConcurrentDictionary<string, UserSession> _sessions = new();
    private readonly ConcurrentDictionary<long, long> _userRoomMapping = new();
    private long _nextRoomId = 1;

    public DungeonLobbyServiceTests()
    {
        _mockRoomRepository = new Mock<IDungeonRoomRepository>();
        _mockSessionRepository = new Mock<IUserSessionRepository>();
        _mockChatSubscriptionService = new Mock<IChatSubscriptionService>();
        _mockDungeonLobbySubscriptionService = new Mock<IDungeonLobbySubscriptionService>();

        SetupMocks();

        _service = new DungeonLobbyService(
            _mockRoomRepository.Object, 
            _mockDungeonLobbySubscriptionService.Object, 
            _mockSessionRepository.Object, 
            _mockChatSubscriptionService.Object);
    }

    private void SetupMocks()
    {
        // IDungeonRoomRepository Setup
        _mockRoomRepository.Setup(r => r.CreateAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long hostId, string roomName, int maxPlayers, CancellationToken ct) =>
            {
                var room = DungeonRoom.Create(roomName, hostId, maxPlayers);
                var roomId = Interlocked.Increment(ref _nextRoomId);
                room.SetRoomId(roomId);
                _rooms[roomId] = room;
                _userRoomMapping[hostId] = roomId;
                return room;
            });

        _mockRoomRepository.Setup(r => r.GetByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long roomId, CancellationToken ct) => _rooms.TryGetValue(roomId, out var room) ? room.Clone() : null);

        _mockRoomRepository.Setup(r => r.GetByUserIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long userId, CancellationToken ct) => 
                _userRoomMapping.TryGetValue(userId, out var roomId) && _rooms.TryGetValue(roomId, out var room) ? room.Clone() : null);

        _mockRoomRepository.Setup(r => r.GetAllActiveRoomsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _rooms.Values.Where(r => r.Status != RoomStatus.Closed).ToList());

        _mockRoomRepository.Setup(r => r.UpdateAsync(It.IsAny<DungeonRoom>(), It.IsAny<CancellationToken>()))
            .Returns((DungeonRoom room, CancellationToken ct) =>
            {
                if (!_rooms.ContainsKey(room.RoomId)) return Task.FromResult(false);
                
                return Task.Run(async () =>
                {
                    // 지연을 추가하여 Race Condition이 발생할 확률을 높임
                    await Task.Delay(10);

                    // 매핑 업데이트
                    foreach (var userId in room.CurrentPlayers)
                    {
                        _userRoomMapping[userId] = room.RoomId;
                    }
                    _rooms[room.RoomId] = room;
                    return true;
                });
            });

        _mockRoomRepository.Setup(r => r.DeleteAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long roomId, CancellationToken ct) =>
            {
                if (!_rooms.TryRemove(roomId, out var room)) return false;
                foreach (var userId in room.CurrentPlayers)
                {
                    _userRoomMapping.TryRemove(userId, out _);
                }
                return true;
            });

        _mockRoomRepository.Setup(r => r.TryJoinRoomAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long userId, long roomId, CancellationToken ct) =>
            {
                // 실제 Redis Lua 스크립트처럼 원자적으로 처리되어야 함
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

                    // 입장 처리 (상태 변경)
                    room.Join(userId);
                    _userRoomMapping[userId] = roomId;
                    
                    return JoinRoomAtomicResult.Success;
                }
            });

        // IUserSessionRepository Setup
        _mockSessionRepository.Setup(s => s.GetBySessionIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string sessionId, CancellationToken ct) => _sessions.TryGetValue(sessionId, out var session) ? session : null);

        _mockSessionRepository.Setup(s => s.UpdateRoomIdAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Callback<string, long, CancellationToken>((sessionId, roomId, ct) =>
            {
                if (_sessions.TryGetValue(sessionId, out var session))
                {
                    session.SetRoomId(roomId);
                }
            })
            .Returns(Task.CompletedTask);
    }

    #region CreateDungeonRoomAsync Tests

    [Fact]
    public async Task 유효한_정보로_던전_방_생성_성공_및_생성자_자동_입장_확인()
    {
        // Arrange
        var sessionId = await CreateTestSession(userId: 1, userName: "user1");
        var roomName = "Test Room";
        var maxPlayers = 4;

        // Act
        var result = await _service.CreateDungeonRoomAsync(sessionId, roomName, maxPlayers);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(roomName, result.Value.RoomName);
        Assert.Equal(1, result.Value.HostUserId);
        Assert.Equal(maxPlayers, result.Value.MaxPlayers);
        Assert.Single(result.Value.CurrentPlayers);
        Assert.Contains(1, result.Value.CurrentPlayers);
    }

    [Fact]
    public async Task 유효하지_않은_세션으로_방_생성_시도_시_실패한다()
    {
        // Arrange
        var invalidSessionId = "invalid-session-id";
        var roomName = "Test Room";
        var maxPlayers = 4;

        // Act
        var result = await _service.CreateDungeonRoomAsync(invalidSessionId, roomName, maxPlayers);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.InvalidRequest, result.InternalErrorCode);
    }

    [Fact]
    public async Task 이미_방에_참여_중인_유저가_새_방을_생성하려고_하면_실패한다()
    {
        // Arrange
        var sessionId = await CreateTestSession(userId: 1, userName: "user1");
        
        // 첫 번째 방 생성
        await _service.CreateDungeonRoomAsync(sessionId, "Room1", 4);

        // Act - 같은 유저가 두 번째 방 생성 시도
        var result = await _service.CreateDungeonRoomAsync(sessionId, "Room2", 4);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.AlreadyInRoom, result.InternalErrorCode);
    }

    #endregion

    #region GetActiveDungeonRoomsAsync Tests

    [Fact]
    public async Task 개설된_방이_하나도_없을_때_활성_방_목록_조회_시_빈_리스트를_반환한다()
    {
        // Act
        var result = await _service.GetActiveDungeonRoomsAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task 여러_개의_방이_존재할_때_모든_활성_방_목록을_성공적으로_조회한다()
    {
        // Arrange
        var session1 = await CreateTestSession(1, "user1");
        var session2 = await CreateTestSession(2, "user2");
        
        await _service.CreateDungeonRoomAsync(session1, "Room1", 4);
        await _service.CreateDungeonRoomAsync(session2, "Room2", 4);

        // Act
        var result = await _service.GetActiveDungeonRoomsAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(2, result.Value.Count());
    }

    [Fact]
    public async Task 종료되거나_닫힌_방은_활성_방_목록_조회_결과에서_제외된다()
    {
        // Arrange
        var session1 = await CreateTestSession(1, "user1");
        var session2 = await CreateTestSession(2, "user2");
        
        var room1Result = await _service.CreateDungeonRoomAsync(session1, "Room1", 4);
        await _service.CreateDungeonRoomAsync(session2, "Room2", 4);
        
        // Room1 닫기 (모든 플레이어 퇴장)
        await _service.LeaveRoomAsync(session1, room1Result.Value!.RoomId);

        // Act
        var result = await _service.GetActiveDungeonRoomsAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
        Assert.Equal("Room2", result.Value!.First().RoomName);
    }

    #endregion

    #region GetDungeonRoomAsync Tests

    [Fact]
    public async Task 방_ID로_존재하는_방의_정보를_성공적으로_조회한다()
    {
        // Arrange
        var sessionId = await CreateTestSession(1, "user1");
        var createResult = await _service.CreateDungeonRoomAsync(sessionId, "Test Room", 4);
        var roomId = createResult.Value!.RoomId;

        // Act
        var result = await _service.GetDungeonRoomAsync(roomId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(roomId, result.Value.RoomId);
        Assert.Equal("Test Room", result.Value.RoomName);
    }

    [Fact]
    public async Task 존재하지_않는_방_ID로_조회_시_방을_찾을_수_없다는_에러를_반환한다()
    {
        // Arrange
        var nonExistentRoomId = 999L;

        // Act
        var result = await _service.GetDungeonRoomAsync(nonExistentRoomId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.RoomNotFound, result.InternalErrorCode);
    }

    #endregion

    #region UpdateRoomSettingsAsync Tests

    [Fact]
    public async Task 방장이_방_이름과_최대_인원_설정을_변경하면_성공적으로_반영된다()
    {
        // Arrange
        var sessionId = await CreateTestSession(1, "user1");
        var createResult = await _service.CreateDungeonRoomAsync(sessionId, "Old Name", 4);
        var roomId = createResult.Value!.RoomId;

        // Act
        var result = await _service.UpdateRoomSettingsAsync(
            sessionId, roomId, "New Name", 3);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("New Name", result.Value!.RoomName);
        Assert.Equal(3, result.Value.MaxPlayers);
    }

    [Fact]
    public async Task 방장이_아닌_일반_플레이어가_방_설정_변경_시도_시_권한_에러로_실패한다()
    {
        // Arrange
        var hostSession = await CreateTestSession(1, "host");
        var otherSession = await CreateTestSession(2, "other");
        
        var createResult = await _service.CreateDungeonRoomAsync(hostSession, "Room", 4);
        var roomId = createResult.Value!.RoomId;

        // Act - 다른 유저가 설정 변경 시도
        var result = await _service.UpdateRoomSettingsAsync(
            otherSession, roomId, "Hacked", 2);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.NotRoomHost, result.InternalErrorCode);
    }

    [Fact]
    public async Task 유효하지_않은_세션으로_방_설정_변경_시도_시_실패한다()
    {
        // Arrange
        var sessionId = await CreateTestSession(1, "user1");
        var createResult = await _service.CreateDungeonRoomAsync(sessionId, "Room", 4);
        var roomId = createResult.Value!.RoomId;

        // Act - 잘못된 세션으로 시도
        var result = await _service.UpdateRoomSettingsAsync(
            "invalid-session", roomId, "New Name", 3);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.InvalidRequest, result.InternalErrorCode);
    }

    [Fact]
    public async Task 방에_참여_중인_인원보다_더_작은_인원수로_최대_인원을_수정하려고_하면_실패한다()
    {
        // Arrange
        var hostSession = await CreateTestSession(1, "host");
        var user2Session = await CreateTestSession(2, "user2");
        var user3Session = await CreateTestSession(3, "user3");
        
        var createResult = await _service.CreateDungeonRoomAsync(hostSession, "Room", 4);
        var roomId = createResult.Value!.RoomId;
        
        await _service.JoinRoomAsync(user2Session, roomId);
        await _service.JoinRoomAsync(user3Session, roomId);

        // Act - 3명 있는데 maxPlayers를 2로 줄이려고 시도
        var result = await _service.UpdateRoomSettingsAsync(
            hostSession, roomId, null, 2);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.UpdateRoomFailed, result.InternalErrorCode);
    }

    #endregion

    #region JoinRoomAsync Tests

    [Fact]
    public async Task 다른_유저가_존재하는_방에_성공적으로_입장하고_플레이어_목록에_추가된다()
    {
        // Arrange
        var hostSession = await CreateTestSession(1, "host");
        var joinSession = await CreateTestSession(2, "joiner");
        
        var createResult = await _service.CreateDungeonRoomAsync(hostSession, "Room", 4);
        var roomId = createResult.Value!.RoomId;

        // Act
        var result = await _service.JoinRoomAsync(joinSession, roomId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.CurrentPlayers.Count);
        Assert.Contains(2, result.Value.CurrentPlayers);
    }

    [Fact]
    public async Task 유효하지_않은_세션으로_방_입장_시도_시_실패한다()
    {
        // Arrange
        var hostSession = await CreateTestSession(1, "host");
        var createResult = await _service.CreateDungeonRoomAsync(hostSession, "Room", 4);
        var roomId = createResult.Value!.RoomId;

        // Act
        var result = await _service.JoinRoomAsync("invalid-session", roomId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.InvalidRequest, result.InternalErrorCode);
    }

    [Fact]
    public async Task 존재하지_않는_방_ID로_입장_시도_시_실패한다()
    {
        // Arrange
        var sessionId = await CreateTestSession(1, "user1");

        // Act
        var result = await _service.JoinRoomAsync(sessionId, 999L);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.RoomNotFound, result.InternalErrorCode);
    }

    [Fact]
    public async Task 유저가_이미_다른_방에_참여_중인_상태에서_새로운_방에_입장하려고_하면_실패한다()
    {
        // Arrange
        var user1Session = await CreateTestSession(1, "user1");
        var user2Session = await CreateTestSession(2, "user2");
        
        var room1Result = await _service.CreateDungeonRoomAsync(user1Session, "Room1", 4);
        var room2Result = await _service.CreateDungeonRoomAsync(user2Session, "Room2", 4);

        // Act - user1이 Room2에 입장 시도 (이미 Room1에 있음)
        var result = await _service.JoinRoomAsync(user1Session, room2Result.Value!.RoomId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.AlreadyInRoom, result.InternalErrorCode);
    }

    [Fact]
    public async Task 최대_인원이_모두_찬_방에_입장하려고_하면_실패한다()
    {
        // Arrange
        var hostSession = await CreateTestSession(1, "host");
        var user2Session = await CreateTestSession(2, "user2");
        var user3Session = await CreateTestSession(3, "user3");
        var user4Session = await CreateTestSession(4, "user4");
        
        var createResult = await _service.CreateDungeonRoomAsync(hostSession, "Room", 2);
        var roomId = createResult.Value!.RoomId;
        
        await _service.JoinRoomAsync(user2Session, roomId);

        // Act - 방이 꽉 찼는데 입장 시도
        var result = await _service.JoinRoomAsync(user3Session, roomId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.JoinRoomFailed, result.InternalErrorCode);
    }

    [Fact]
    public async Task 이미_해당_방에_참여_중인_유저가_다시_입장하려고_하면_실패한다()
    {
        // Arrange
        var sessionId = await CreateTestSession(1, "user1");
        var createResult = await _service.CreateDungeonRoomAsync(sessionId, "Room", 4);
        var roomId = createResult.Value!.RoomId;

        // Act - 이미 방에 있는데 다시 입장 시도
        var result = await _service.JoinRoomAsync(sessionId, roomId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.AlreadyInRoom, result.InternalErrorCode);
    }

    [Fact]
    public async Task 대기_상태가_아닌_방에_입장하려고_하면_실패한다()
    {
        // Arrange
        var hostSession = await CreateTestSession(1, "host");
        var user2Session = await CreateTestSession(2, "user2");
        var user3Session = await CreateTestSession(3, "user3");
        
        var createResult = await _service.CreateDungeonRoomAsync(hostSession, "Playing Room", 4);
        var roomId = createResult.Value!.RoomId;
        
        await _service.JoinRoomAsync(user2Session, roomId);
        
        // 게임 시작 (상태를 Playing으로 변경)
        await _service.StartGameAsync(hostSession, roomId);

        // Act - 게임 중인 방에 입장 시도
        var result = await _service.JoinRoomAsync(user3Session, roomId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.JoinRoomFailed, result.InternalErrorCode);
        Assert.Equal("입장 가능한 방 상태가 아닙니다.", result.Message);
    }

    [Fact]
    public async Task 여러_유저가_동시에_한_방에_입장을_시도할_때_최대_인원_제한이_지켜져야_한다()
    {
        // Arrange
        var maxPlayers = 2;
        var hostSession = await CreateTestSession(1, "host");
        var roomResult = await _service.CreateDungeonRoomAsync(hostSession, "Concurrent Room", maxPlayers);
        var roomId = roomResult.Value!.RoomId;

        // 동시에 입장 시도할 유저들 (이미 방장이 있으므로 1명만 더 들어올 수 있음)
        var playerSessions = new List<string>();
        for (int i = 2; i <= 10; i++)
        {
            playerSessions.Add(await CreateTestSession(i, $"user{i}"));
        }

        // Act
        var tasks = playerSessions.Select(session => _service.JoinRoomAsync(session, roomId)).ToList();
        var results = await Task.WhenAll(tasks);

        // Assert
        var successCount = results.Count(r => r.IsSuccess);
        var room = await _service.GetDungeonRoomAsync(roomId);

        // 방장(1명) + 추가 성공(1명) = 총 2명이어야 함 (maxPlayers)
        Assert.Equal(maxPlayers - 1, successCount); 
        Assert.Equal(maxPlayers, room.Value!.CurrentPlayers.Count);
    }

    #endregion

    #region LeaveRoomAsync Tests

    [Fact]
    public async Task 참여_중인_방에서_성공적으로_퇴장하고_플레이어_목록에서_제거된다()
    {
        // Arrange
        var hostSession = await CreateTestSession(1, "host");
        var joinSession = await CreateTestSession(2, "joiner");
        
        var createResult = await _service.CreateDungeonRoomAsync(hostSession, "Room", 4);
        var roomId = createResult.Value!.RoomId;
        
        await _service.JoinRoomAsync(joinSession, roomId);

        // Act
        var result = await _service.LeaveRoomAsync(joinSession, roomId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!.CurrentPlayers);
        Assert.DoesNotContain(2, result.Value.CurrentPlayers);
    }

    [Fact]
    public async Task 유효하지_않은_세션으로_방_퇴장_시도_시_실패한다()
    {
        // Arrange
        var sessionId = await CreateTestSession(1, "user1");
        var createResult = await _service.CreateDungeonRoomAsync(sessionId, "Room", 4);
        var roomId = createResult.Value!.RoomId;

        // Act
        var result = await _service.LeaveRoomAsync("invalid-session", roomId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.InvalidRequest, result.InternalErrorCode);
    }

    [Fact]
    public async Task 방에_참여하지_않은_유저가_해당_방에서_퇴장하려고_하면_실패한다()
    {
        // Arrange
        var hostSession = await CreateTestSession(1, "host");
        var otherSession = await CreateTestSession(2, "other");
        
        var createResult = await _service.CreateDungeonRoomAsync(hostSession, "Room", 4);
        var roomId = createResult.Value!.RoomId;

        // Act - 방에 없는 유저가 퇴장 시도
        var result = await _service.LeaveRoomAsync(otherSession, roomId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.NotInRoom, result.InternalErrorCode);
    }

    [Fact]
    public async Task 방에_혼자_남은_마지막_플레이어가_퇴장하면_방이_자동으로_닫히고_삭제된다()
    {
        // Arrange
        var sessionId = await CreateTestSession(1, "user1");
        var createResult = await _service.CreateDungeonRoomAsync(sessionId, "Room", 4);
        var roomId = createResult.Value!.RoomId;

        // Act
        var result = await _service.LeaveRoomAsync(sessionId, roomId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(RoomStatus.Closed, result.Value!.Status);
        
        // 방이 삭제되었는지 확인
        var getResult = await _service.GetDungeonRoomAsync(roomId);
        Assert.False(getResult.IsSuccess);
        Assert.Equal(ErrorCodes.RoomNotFound, getResult.InternalErrorCode);
    }

    [Fact]
    public async Task 방장이_퇴장하면_남아있는_플레이어_중_한_명이_새로운_방장으로_선출된다()
    {
        // Arrange
        var hostSession = await CreateTestSession(1, "host");
        var user2Session = await CreateTestSession(2, "user2");
        
        var createResult = await _service.CreateDungeonRoomAsync(hostSession, "Room", 4);
        var roomId = createResult.Value!.RoomId;
        
        await _service.JoinRoomAsync(user2Session, roomId);

        // Act - 방장이 퇴장
        var result = await _service.LeaveRoomAsync(hostSession, roomId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.HostUserId); // user2가 새 방장
        Assert.Single(result.Value.CurrentPlayers);
        Assert.Contains(2, result.Value.CurrentPlayers);
    }

    #endregion

    #region StartGameAsync Tests

    [Fact]
    public async Task 방장이_게임_시작을_요청하면_방_상태가_게임_중으로_변경된다()
    {
        // Arrange
        var hostSession = await CreateTestSession(1, "host");
        var user2Session = await CreateTestSession(2, "user2");
        
        var createResult = await _service.CreateDungeonRoomAsync(hostSession, "Room", 4);
        var roomId = createResult.Value!.RoomId;
        
        await _service.JoinRoomAsync(user2Session, roomId);

        // Act
        var result = await _service.StartGameAsync(hostSession, roomId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(RoomStatus.Playing, result.Value!.Status);
    }

    [Fact]
    public async Task 유효하지_않은_세션으로_게임_시작_시도_시_실패한다()
    {
        // Arrange
        var sessionId = await CreateTestSession(1, "user1");
        var createResult = await _service.CreateDungeonRoomAsync(sessionId, "Room", 4);
        var roomId = createResult.Value!.RoomId;

        // Act
        var result = await _service.StartGameAsync("invalid-session", roomId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.InvalidRequest, result.InternalErrorCode);
    }

    [Fact]
    public async Task 방장이_아닌_일반_플레이어가_게임_시작_시도_시_권한_에러로_실패한다()
    {
        // Arrange
        var hostSession = await CreateTestSession(1, "host");
        var otherSession = await CreateTestSession(2, "other");
        
        var createResult = await _service.CreateDungeonRoomAsync(hostSession, "Room", 4);
        var roomId = createResult.Value!.RoomId;
        
        await _service.JoinRoomAsync(otherSession, roomId);

        // Act - 방장이 아닌 사람이 게임 시작 시도
        var result = await _service.StartGameAsync(otherSession, roomId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.NotRoomHost, result.InternalErrorCode);
    }

    [Fact]
    public async Task 게임_시작에_필요한_최소_인원_미만일_때_게임_시작을_시도하면_실패한다()
    {
        // Arrange
        var sessionId = await CreateTestSession(1, "user1");
        var createResult = await _service.CreateDungeonRoomAsync(sessionId, "Room", 4);
        var roomId = createResult.Value!.RoomId;

        // Act - 혼자서 게임 시작 시도
        var result = await _service.StartGameAsync(sessionId, roomId);

        // Assert
        Assert.False(result.IsSuccess);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// 테스트용 세션 생성
    /// </summary>
    private async Task<string> CreateTestSession(long userId, string userName)
    {
        var email = $"{userName}@example.com";
        var publicId = $"PUB{userId:D8}";
        var sessionId = Guid.NewGuid().ToString();
        var session = UserSession.Create(userId, email, userName, publicId, sessionId);
        _sessions[sessionId] = session;
        return sessionId;
    }

    #endregion
}