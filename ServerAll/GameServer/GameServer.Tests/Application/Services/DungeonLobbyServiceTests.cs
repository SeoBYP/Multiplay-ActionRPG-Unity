using GameServer.Application.Common;
using GameServer.Application.Services.DungeonLobby;
using GameServer.Domain.Entities;
using GameServer.Tests.Fakes;

namespace GameServer.Tests.Application.Services;

public class DungeonLobbyServiceTests
{
    private readonly IDungeonRoomRepository _repository;
    private readonly DungeonLobbyService _service;

    public DungeonLobbyServiceTests()
    {
        _repository = new FakeDungeonRoomRepository();
        _service = new DungeonLobbyService(_repository);
    }

    [Fact]
    public async Task CreateDungeonRoomAsync_는_새로운_방을_생성한다()
    {
        // given
        var userId = 1L;
        var roomName = "초보자방";
        var maxPlayers = 4;

        // when
        var result = await _service.CreateDungeonRoomAsync(userId, roomName, maxPlayers);
        
        // then
        var room = result.Value;
        Assert.True(room.RoomId > 0);
        Assert.Equal(roomName, room.RoomName);
        Assert.Equal(userId, room.HostUserId);
        Assert.Equal(maxPlayers, room.MaxPlayers);
        Assert.Equal(RoomStatus.Waiting, room.Status);
        Assert.Single(room.CurrentPlayers);
        Assert.Contains(userId, room.CurrentPlayers);
    }

    [Fact]
    public async Task CreateDungeonRoomAsync_는_이미_방에_있는_유저면_실패한다()
    {
        // given
        var userId = 1L;
        await _service.CreateDungeonRoomAsync(userId, "첫번째방", 4);
        
        // when - 같은 유저가 또 방 생성 시도
        var result = await _service.CreateDungeonRoomAsync(userId, "두번째방", 4);

        // then
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorMessages.AlreadyInRoom, result.Message);
    }

    [Fact]
    public async Task CreateDungeonRoomAsync_는_커스텀_MaxPlayers로_방을_생성한다()
    {
        // given
        var userId = 1L;
        var maxPlayers = 2;

        // when
        var result = await _service.CreateDungeonRoomAsync(userId, "2인방", maxPlayers);

        // then
        Assert.True(result.IsSuccess);
        Assert.Equal(maxPlayers, result.Value!.MaxPlayers);
    }

    [Fact]
    public async Task GetActiveDungeonRoomsAsync_는_모든_활성_방을_반환한다()
    {
        // given - 3개 방 생성
        await _service.CreateDungeonRoomAsync(1L, "방1", 4);
        await _service.CreateDungeonRoomAsync(2L, "방2", 4);
        await _service.CreateDungeonRoomAsync(3L, "방3", 4);

        // when
        var result = await _service.GetActiveDungeonRoomsAsync();

        // then
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(3, result.Value.Count());
    }

    [Fact]
    public async Task GetActiveDungeonRoomsAsync_는_Closed_상태_방은_제외한다()
    {
        // given - 방 생성 후 모든 플레이어 퇴장 (Closed)
        var createResult = await _service.CreateDungeonRoomAsync(1L, "방1", 4);
        var roomId = createResult.Value!.RoomId;
        
        await _service.LeaveRoomAsync(1L, roomId);  // 방장 퇴장 → Closed

        // when
        var result = await _service.GetActiveDungeonRoomsAsync();

        // then
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);  // Closed 방은 조회 안 됨
    }

    [Fact]
    public async Task GetActiveDungeonRoomsAsync_는_방이_없으면_빈_목록을_반환한다()
    {
        // given - 방 없음

        // when
        var result = await _service.GetActiveDungeonRoomsAsync();

        // then
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task GetDungeonRoomAsync_는_방_정보를_조회한다()
    {
        // given
        var createResult = await _service.CreateDungeonRoomAsync(1L, "테스트방", 4);
        var roomId = createResult.Value!.RoomId;

        // when
        var result = await _service.GetDungeonRoomAsync(roomId);

        // then
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(roomId, result.Value.RoomId);
        Assert.Equal("테스트방", result.Value.RoomName);
    }
    
    [Fact]
    public async Task GetDungeonRoomAsync_는_존재하지않는_방이면_실패한다()
    {
        // given
        var nonExistentRoomId = 999L;

        // when
        var result = await _service.GetDungeonRoomAsync(nonExistentRoomId);

        // then
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorMessages.RoomNotFound, result.Message);
    }

    [Fact]
    public async Task JoinRoomAsync_는_방에_정상적으로_입장한다()
    {
        // given
        var hostId = 1L;
        var userId = 2L;
        var createResult = await _service.CreateDungeonRoomAsync(hostId, "테스트방", 4);
        var roomId = createResult.Value!.RoomId;

        // when
        var result = await _service.JoinRoomAsync(userId, roomId);

        // then
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(2, result.Value.GetPlayerCount());
        Assert.Contains(userId, result.Value.CurrentPlayers);
    }

    [Fact]
    public async Task JoinRoomAsync_는_이미_다른_방에_있으면_실패한다()
    {
        // given
        var userId = 2L;
        var room1Result = await _service.CreateDungeonRoomAsync(1L, "방1", 4);
        var room2Result = await _service.CreateDungeonRoomAsync(3L, "방2", 4);
        
        var room1Id = room1Result.Value!.RoomId;
        var room2Id = room2Result.Value!.RoomId;
        
        await _service.JoinRoomAsync(userId, room1Id);  // 방1에 입장

        // when - 방2에 입장 시도
        var result = await _service.JoinRoomAsync(userId, room2Id);

        // then
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorMessages.AlreadyInRoom, result.Message);
    }

    [Fact]
    public async Task JoinRoomAsync_는_존재하지않는_방이면_실패한다()
    {
        // given
        var userId = 2L;
        var roomId = 999L;

        // when
        var result = await _service.JoinRoomAsync(userId, roomId);

        // then
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorMessages.RoomNotFound, result.Message);
    }

    [Fact]
    public async Task JoinRoomAsync_는_방이_가득_차면_실패한다()
    {
        // given - 2인 방 생성
        var createResult = await _service.CreateDungeonRoomAsync(1L, "2인방", 2);
        var roomId = createResult.Value!.RoomId;
        
        await _service.JoinRoomAsync(2L, roomId);  // 2명 모두 입장

        // when - 3번째 유저 입장 시도
        var result = await _service.JoinRoomAsync(3L, roomId);

        // then
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.JoinRoomFailed, result.InternalErrorCode);
    }

    [Fact]
    public async Task JoinRoomAsync_는_같은_방에_중복_입장을_불허용한다()
    {
        // given
        var userId = 2L;
        var createResult = await _service.CreateDungeonRoomAsync(1L, "테스트방", 4);
        var roomId = createResult.Value!.RoomId;
        
        await _service.JoinRoomAsync(userId, roomId);  // 첫 입장

        // when - 같은 방에 다시 입장 (재접속 시나리오)
        var result = await _service.JoinRoomAsync(userId, roomId);

        // then - 실패
        Assert.True(!result.IsSuccess);
        Assert.Equal(ErrorCodes.AlreadyInRoom, result.InternalErrorCode);
    }

    [Fact]
    public async Task LeaveRoomAsync_는_방에서_정상적으로_퇴장한다()
    {
        // given
        var userId = 2L;
        var createResult = await _service.CreateDungeonRoomAsync(1L, "테스트방", 4);
        var roomId = createResult.Value!.RoomId;
        
        await _service.JoinRoomAsync(userId, roomId);

        // when
        var result = await _service.LeaveRoomAsync(userId, roomId);

        // then
        Assert.True(result.IsSuccess);
        
        // 방 확인
        var room = await _service.GetDungeonRoomAsync(roomId);
        Assert.Equal(1, room.Value!.GetPlayerCount());
        Assert.DoesNotContain(userId, room.Value.CurrentPlayers);
    }

    [Fact]
    public async Task LeaveRoomAsync_는_존재하지않는_방이면_실패한다()
    {
        // given
        var userId = 1L;
        var roomId = 999L;

        // when
        var result = await _service.LeaveRoomAsync(userId, roomId);

        // then
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorMessages.RoomNotFound, result.Message);
    }

    [Fact]
    public async Task LeaveRoomAsync_는_방에_없는_유저면_실패한다()
    {
        // given
        var createResult = await _service.CreateDungeonRoomAsync(1L, "테스트방", 4);
        var roomId = createResult.Value!.RoomId;
        
        var userId = 999L;  // 방에 없는 유저

        // when
        var result = await _service.LeaveRoomAsync(userId, roomId);

        // then
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorMessages.NotInRoom, result.Message);
    }

    [Fact]
    public async Task LeaveRoomAsync_는_방장이_퇴장하면_다음_플레이어가_방장이_된다()
    {
        // given
        var hostId = 1L;
        var userId = 2L;
        var createResult = await _service.CreateDungeonRoomAsync(hostId, "테스트방", 4);
        var roomId = createResult.Value!.RoomId;
        
        await _service.JoinRoomAsync(userId, roomId);

        // when - 방장 퇴장
        await _service.LeaveRoomAsync(hostId, roomId);

        // then - userId가 새 방장
        var room = await _service.GetDungeonRoomAsync(roomId);
        Assert.Equal(userId, room.Value!.HostUserId);
    }

    [Fact]
    public async Task LeaveRoomAsync_는_모든_플레이어가_퇴장하면_방이_삭제된다()
    {
        // given
        var hostId = 1L;
        var createResult = await _service.CreateDungeonRoomAsync(hostId, "테스트방", 4);
        var roomId = createResult.Value!.RoomId;

        // when - 방장 퇴장 (유일한 플레이어)
        await _service.LeaveRoomAsync(hostId, roomId);

        // then - 방이 삭제됨
        var room = await _service.GetDungeonRoomAsync(roomId);
        Assert.False(room.IsSuccess);
        Assert.Equal(ErrorMessages.RoomNotFound, room.Message);
    }
    
    [Fact]
    public async Task StartGameAsync_는_게임을_정상적으로_시작한다()
    {
        // given
        var hostId = 1L;
        var createResult = await _service.CreateDungeonRoomAsync(hostId, "테스트방", 4);
        var roomId = createResult.Value!.RoomId;
        
        await _service.JoinRoomAsync(2L, roomId);  // 2명 이상 필요

        // when
        var result = await _service.StartGameAsync(hostId, roomId);

        // then
        Assert.True(result.IsSuccess);
        
        // 방 상태 확인
        var room = await _service.GetDungeonRoomAsync(roomId);
        Assert.Equal(RoomStatus.Playing, room.Value!.Status);
    }

    [Fact]
    public async Task StartGameAsync_는_존재하지않는_방이면_실패한다()
    {
        // given
        var userId = 1L;
        var roomId = 999L;

        // when
        var result = await _service.StartGameAsync(userId, roomId);

        // then
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorMessages.RoomNotFound, result.Message);
    }

    [Fact]
    public async Task StartGameAsync_는_방장이_아니면_실패한다()
    {
        // given
        var hostId = 1L;
        var userId = 2L;
        var createResult = await _service.CreateDungeonRoomAsync(hostId, "테스트방", 4);
        var roomId = createResult.Value!.RoomId;
        await _service.JoinRoomAsync(userId, roomId);
        // when
        var result = await _service.StartGameAsync(userId, roomId);
        
        // then
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorMessages.NotRoomHost, result.Message);
    }


    [Fact]
    public async Task StartGameAsync_는_플레이어가_1명이면_실패한다()
    {
        // given
        var hostId = 1L;
        var createResult = await _service.CreateDungeonRoomAsync(hostId, "테스트방", 4);
        var roomId = createResult.Value!.RoomId;

        // when - 방장 혼자 시작 시도
        var result = await _service.StartGameAsync(hostId, roomId);

        // then
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task 전체_플로우_방생성_입장_퇴장_삭제()
    {
        // 1. 방생성
        var createResult = await _service.CreateDungeonRoomAsync(1L, "테스트방", 4);
        Assert.True(createResult.IsSuccess);
        var roomId = createResult.Value!.RoomId;
        
        // 2. 방 입장
        var joinResult = await _service.JoinRoomAsync(2L, roomId);
        Assert.True(joinResult.IsSuccess);
        Assert.Equal(2, joinResult.Value!.GetPlayerCount());
        
        // 3. 플레이어 퇴장
        var leaveResult = await _service.LeaveRoomAsync(2L, roomId);
        Assert.True(leaveResult.IsSuccess);
        
        // 4. 방장 퇴장 (방 삭제)
        await _service.LeaveRoomAsync(1L, roomId);
        
        // 5. 방이 삭제되었는지 확인
        var getResult = await _service.GetDungeonRoomAsync(roomId);
        Assert.False(getResult.IsSuccess);
    }

    [Fact]
    public async Task 전체_플로우_방생성_입장_게임시작()
    {
        // 1. 방 생성
        var createResult = await _service.CreateDungeonRoomAsync(1L, "레이드방", 4);
        var roomId = createResult.Value!.RoomId;

        // 2. 플레이어들 입장
        await _service.JoinRoomAsync(2L, roomId);
        await _service.JoinRoomAsync(3L, roomId);
        await _service.JoinRoomAsync(4L, roomId);

        // 3. 방 가득 참 확인
        var room = await _service.GetDungeonRoomAsync(roomId);
        Assert.True(room.Value!.IsFull);

        // 4. 게임 시작
        var startResult = await _service.StartGameAsync(1L, roomId);
        Assert.True(startResult.IsSuccess);

        // 5. 상태 확인
        room = await _service.GetDungeonRoomAsync(roomId);
        Assert.Equal(RoomStatus.Playing, room.Value!.Status);
    }
}