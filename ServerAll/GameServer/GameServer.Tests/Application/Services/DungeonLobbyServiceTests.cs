using GameServer.Application.Common;
using GameServer.Application.Services.Chat.Interfaces;
using GameServer.Application.Services.DungeonLobby;
using GameServer.Domain.Entities;
using GameServer.Infrastructure.Interfaces.DungeonRoom;
using GameServer.Infrastructure.Interfaces.User;
using GameServer.Tests.Fakes;
using GameServer.Tests.Infrastructure;

namespace GameServer.Tests.Application.Services;

public class DungeonLobbyServiceTests
{
    private readonly IUserSessionRepository _sessionRepository;
    private readonly DungeonLobbyService _service;

    public DungeonLobbyServiceTests()
    {
        IDungeonRoomRepository roomRepository = new FakeDungeonRoomRepository();
        _sessionRepository = new FakeUserSessionRepository();
        IChatSubscriptionService chatSubscriptionService = new FakeChatSubscriptionService();
        _service = new DungeonLobbyService(roomRepository, _sessionRepository, chatSubscriptionService);
    }

    #region CreateDungeonRoomAsync Tests

    [Fact]
    public async Task 던전_방_생성_성공()
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
    public async Task 잘못된_세션으로_방_생성_실패()
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
    public async Task 이미_방에_있는_유저의_방_생성_실패()
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
    public async Task 방이_없을_때_활성_방_목록_빈_리스트_반환()
    {
        // Act
        var result = await _service.GetActiveDungeonRoomsAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task 여러_방이_있을_때_모든_활성_방_반환()
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
    public async Task 닫힌_방은_목록에서_제외()
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
    public async Task 존재하는_방_조회_성공()
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
    public async Task 존재하지_않는_방_조회_실패()
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
    public async Task 방장이_방_설정_변경_성공()
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
    public async Task 방장이_아닌_경우_설정_변경_실패()
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
    public async Task 잘못된_세션으로_설정_변경_실패()
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
    public async Task 현재_인원보다_적게_최대_인원_수정_시_실패()
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
    public async Task 방_입장_성공()
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
    public async Task 잘못된_세션으로_방_입장_실패()
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
    public async Task 존재하지_않는_방_입장_실패()
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
    public async Task 이미_다른_방에_있을_때_입장_실패()
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
    public async Task 가득_찬_방_입장_실패()
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
    public async Task 이미_해당_방에_있을_때_입장_실패()
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

    #endregion

    #region LeaveRoomAsync Tests

    [Fact]
    public async Task 방_퇴장_성공()
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
    public async Task 잘못된_세션으로_방_퇴장_실패()
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
    public async Task 방에_없을_때_퇴장_시도_실패()
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
    public async Task 마지막_플레이어_퇴장_시_방_닫힘()
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
    public async Task 방장_퇴장_시_새로운_방장_선출()
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
    public async Task 게임_시작_성공()
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
    public async Task 잘못된_세션으로_게임_시작_실패()
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
    public async Task 방장이_아닌_경우_게임_시작_실패()
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
    public async Task 인원이_부족할_때_게임_시작_실패()
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
        var session = await _sessionRepository.CreateSessionAsync(userId, userName, email, publicId);
        return session!.SessionId;
    }

    #endregion
}