using GameServer.Domain.Entities;
namespace GameServer.Tests.Domain.Entities;

public class DungeonRoomTests
{
    // ========================================
    // Create 테스트
    // ========================================
    
    [Fact]
    public void Create_는_유효한_파라미터로_던전룸을_생성한다()
    {
        // given
        var roomName = "testRoom";
        var hostId = 1;
        var maxPlayers = 4;
        
        // when
        var room = DungeonRoom.Create(roomName, hostId, maxPlayers);
       
        // then
        Assert.NotNull(room);
        Assert.Equal(roomName, room.RoomName);
        Assert.Equal(hostId, room.HostUserId);
        Assert.Equal(maxPlayers, room.MaxPlayers);
        Assert.Equal(1, room.GetPlayerCount());
        Assert.Equal(RoomStatus.Waiting, room.Status);
    }

    [Fact]
    public void Create_는_roomName이_빈문자열이면_예외를_던진다()
    {
        // given
        var roomName = "";
        var hostId = 1;
        
        // when & then
        Assert.Throws<ArgumentException>(() => DungeonRoom.Create(roomName, hostId, 4));
    }
    
    [Fact]
    public void Create_는_roomName이_null이면_예외를_던진다()
    {
        // given
        string roomName = null;
        var hostId = 1;
        
        // when & then
        Assert.Throws<ArgumentException>(() => DungeonRoom.Create(roomName, hostId,4));
    }
    
    [Fact]
    public void Create_는_roomName이_공백만_있으면_예외를_던진다()
    {
        // given
        var roomName = "   ";
        var hostId = 1;
        
        // when & then
        Assert.Throws<ArgumentException>(() => DungeonRoom.Create(roomName, hostId,4));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_는_hostUserId가_0이하면_예외를_던진다(long hostId)
    {
        // given
        var roomName = "testRoom";
        
        // when & then
        Assert.Throws<ArgumentException>(() => DungeonRoom.Create(roomName, hostId, 4));
    }
    
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_는_maxPlayers가_0이하면_예외를_던진다(int maxPlayers)
    {
        // given
        var roomName = "testRoom";
        var hostId = 1;
        
        // when & then
        Assert.Throws<ArgumentException>(() => DungeonRoom.Create(roomName, hostId, maxPlayers));
    }
    
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(8)]
    public void Create_는_maxPlayers를_커스텀_설정할_수_있다(int maxPlayers)
    {
        // given
        var roomName = "testRoom";
        var hostId = 1;
        
        // when
        var room = DungeonRoom.Create(roomName, hostId, maxPlayers);
        
        // then
        Assert.Equal(maxPlayers, room.MaxPlayers);
    }
    
    // ========================================
    // Join 테스트
    // ========================================
    
    [Fact]
    public void Join_은_유효한_userId로_플레이어를_추가한다()
    {
        // given
        var room = DungeonRoom.Create("testRoom", hostUserId: 1,4);
        var playerId = 2;
        
        // when
        room.Join(playerId);
        
        // then
        Assert.True(room.IsExist(playerId));
        Assert.Equal(2, room.GetPlayerCount());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Join_은_userId가_0이하면_예외를_던진다(long playerId)
    {
        // given
        var room = DungeonRoom.Create("testRoom", hostUserId: 1,4);
        
        // when & then
        Assert.Throws<ArgumentException>(() => room.Join(playerId));
    }

    [Fact]
    public void Join_은_중복_입장을_방지한다()
    {
        // given
        var room = DungeonRoom.Create("testRoom", hostUserId: 1,4);
        
        // when & then - 방장(userId=1) 중복 입장 시도
        Assert.Throws<InvalidOperationException>(() => room.Join(1));
    }
    
    [Fact]
    public void Join_은_방이_가득_차면_예외를_던진다()
    {
        // given
        var room = DungeonRoom.Create("testRoom", hostUserId: 1,4);
        room.Join(2);
        room.Join(3);
        room.Join(4);
    
        // when & then - 5번째 입장 시도
        Assert.Throws<InvalidOperationException>(() => room.Join(5));
    }

    [Fact]
    public void Join_은_Playing_상태에서는_입장_불가()
    {
        // given
        var room = DungeonRoom.Create("testRoom", hostUserId: 1,4);
        room.Join(2);
        room.StartGame(1);
    
        // when & then
        Assert.Throws<InvalidOperationException>(() => room.Join(3));
    }
    
    [Fact]
    public void Join_은_Closed_상태에서는_입장_불가()
    {
        // given
        var room = DungeonRoom.Create("testRoom", hostUserId: 1,4);
        room.Leave(1); // 방장 퇴장 -> Closed
    
        // when & then
        Assert.Throws<InvalidOperationException>(() => room.Join(2));
    }
    
    // ========================================
    // Leave 테스트
    // ========================================
    
    [Fact]
    public void Leave_는_플레이어를_정상적으로_제거한다()
    {
        // given
        var room = DungeonRoom.Create("testRoom", hostUserId: 1,4);
        var playerId = 2;
        room.Join(playerId);
        
        // when
        room.Leave(playerId);
        
        // then
        Assert.False(room.IsExist(playerId));
        Assert.Equal(1, room.GetPlayerCount());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Leave_는_userId가_0이하면_예외를_던진다(long playerId)
    {
        // given
        var room = DungeonRoom.Create("testRoom", hostUserId: 1,4);
        
        // when & then
        Assert.Throws<ArgumentException>(() => room.Leave(playerId));
    }

    [Fact]
    public void Leave_는_존재하지_않는_플레이어면_예외를_던진다()
    {
        // given
        var room = DungeonRoom.Create("testRoom", hostUserId: 1,4);
        room.Join(2);
        
        // when & then
        Assert.Throws<InvalidOperationException>(() => room.Leave(3));
    }

    [Fact]
    public void Leave_는_방장이_떠나면_다음_플레이어를_방장으로_임명한다()
    {
        // given
        var room = DungeonRoom.Create("testRoom", hostUserId: 1,4);
        room.Join(2);
        room.Join(3);
        room.Join(4);
        
        // when
        room.Leave(1);
        
        // then - 첫 번째 플레이어(userId=2)가 방장
        Assert.True(room.IsHost(2));
        Assert.Equal(3, room.GetPlayerCount());
    }

    [Fact]
    public void Leave_는_모든_플레이어가_떠나면_Closed_상태가_된다()
    {
        // given
        var room = DungeonRoom.Create("testRoom", hostUserId: 1,4);
        
        // when
        room.Leave(1);
        
        // then
        Assert.True(room.IsEmpty);
        Assert.Equal(RoomStatus.Closed, room.Status);
    }
    
    [Fact]
    public void Leave_는_Playing_상태에서도_퇴장_가능하다()
    {
        // given
        var room = DungeonRoom.Create("testRoom", hostUserId: 1,4);
        room.Join(2);
        room.StartGame(1);
        
        // when
        room.Leave(2);
        
        // then
        Assert.False(room.IsExist(2));
        Assert.Equal(1, room.GetPlayerCount());
    }
    
    [Fact]
    public void Leave_는_방장_외_모든_플레이어가_나가도_방장은_유지된다()
    {
        // given
        var room = DungeonRoom.Create("testRoom", hostUserId: 1,4);
        room.Join(2);
        room.Join(3);
        
        // when
        room.Leave(2);
        room.Leave(3);
        
        // then
        Assert.True(room.IsHost(1));
        Assert.Equal(1, room.GetPlayerCount());
        Assert.Equal(RoomStatus.Waiting, room.Status); // Closed 아님!
    }

    // ========================================
    // UpdateRoomName 테스트
    // ========================================

    [Theory]
    [InlineData("testRoom2")]
    [InlineData("고수방")]
    [InlineData("초보방")]
    public void UpdateRoomSettings_은_방장이면_방이름을_변경할_수_있다(string newRoomName)
    {
        // given
        var room = DungeonRoom.Create("testRoom", hostUserId: 1,4);
        
        // when
        room.UpdateRoomSettings(1, roomName:newRoomName);
        
        // then
        Assert.Equal(newRoomName, room.RoomName);
    }
    
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(10)]
    public void UpdateRoomSettings_은_방장이면_최대플레이어_수를_변경할_수_있다(int maxPlayer)
    {
        // given
        var room = DungeonRoom.Create("testRoom", hostUserId: 1,4);
        
        // when
        room.UpdateRoomSettings(1, maxPlayers:maxPlayer);
        
        // then
        Assert.Equal(maxPlayer, room.MaxPlayers);
    }
    
    [Fact]
    public void UpdateRoomSettings_은_방장이_아니면_예외를_던진다()
    {
        // given
        var room = DungeonRoom.Create("testRoom", hostUserId: 1,4);
        room.Join(2);
        
        // when & then
        Assert.Throws<UnauthorizedAccessException>(() => 
            room.UpdateRoomSettings(2, roomName:"testRoom2"));
    }
    
    [Fact]
    public void UpdateRoomSettings_은_빈_문자열이면_예외를_던진다()
    {
        // given
        var room = DungeonRoom.Create("testRoom", hostUserId: 1,4);
        
        // when & then
        Assert.Throws<ArgumentException>(() => 
            room.UpdateRoomSettings(1, ""));
    }
    
    [Fact]
    public void UpdateRoomSettings_은_Playing_상태에서는_변경_불가()
    {
        // given
        var room = DungeonRoom.Create("testRoom", hostUserId: 1,4);
        room.Join(2);
        room.StartGame(1);
        
        // when & then
        Assert.Throws<InvalidOperationException>(() => 
            room.UpdateRoomSettings(1, "testRoom2"));
    }
    
    [Fact]
    public async Task UpdateRoomSettings_는_현재_플레이어보다_작게_변경하면_실패한다()
    {
        // given
        var hostId = 1L;
        var room = DungeonRoom.Create("testRoom", hostUserId: hostId,4);
    
        room.Join(2L);
        room.Join(3L);  // 총 3명

        // when & then
        Assert.Throws<InvalidOperationException>(() => 
            room.UpdateRoomSettings(hostId, maxPlayers: 2));
    }
    
    [Fact]
    public void UpdateRoomSettings_은_MaxPlayers_는_2미만으로_변경_불가()
    {
        // given
        var room = DungeonRoom.Create("testRoom", hostUserId: 1,4);
        
        // when & then
        Assert.Throws<ArgumentException>(() => 
            room.UpdateRoomSettings(1, maxPlayers:1));
    }

    // ========================================
    // StartGame 테스트
    // ========================================

    [Fact]
    public void StartGame_은_방장이면_게임을_시작할_수_있다()
    {
        // given
        var room = DungeonRoom.Create("testRoom", hostUserId: 1,4);
        room.Join(2);
        
        // when
        room.StartGame(1);
        
        // then
        Assert.Equal(RoomStatus.Playing, room.Status);
    }

    [Fact]
    public void StartGame_은_방장이_아니면_예외를_던진다()
    {
        // given
        var room = DungeonRoom.Create("testRoom", hostUserId: 1,4);
        room.Join(2);
    
        // when & then
        Assert.Throws<UnauthorizedAccessException>(() => room.StartGame(2));
    }
    
    [Fact]
    public void StartGame_은_플레이어가_1명이면_예외를_던진다()
    {
        // given
        var room = DungeonRoom.Create("testRoom", hostUserId: 1,4);
        
        // when & then
        Assert.Throws<InvalidOperationException>(() => room.StartGame(1));
    }

    [Fact]
    public void StartGame_은_이미_Playing_상태면_예외를_던진다()
    {
        // given
        var room = DungeonRoom.Create("testRoom", hostUserId: 1,4);
        room.Join(2);
        room.StartGame(1);
        
        // when & then
        Assert.Throws<InvalidOperationException>(() => room.StartGame(1));
    }
    
    [Fact]
    public void StartGame_은_Closed_상태에서는_시작_불가()
    {
        // given
        var room = DungeonRoom.Create("testRoom", hostUserId: 1,4);
        room.Join(2);
        room.Leave(1); // 방장 퇴장 -> 방장 위임
        room.Leave(2); // 모두 퇴장 -> Closed
        
        // when & then
        Assert.Throws<InvalidOperationException>(() => room.StartGame(2));
    }
    
    // ========================================
    // 엣지 케이스 테스트
    // ========================================
    
    [Fact]
    public void Join_후_즉시_Leave_하면_정상_동작한다()
    {
        // given
        var room = DungeonRoom.Create("testRoom", hostUserId: 1,4);
        
        // when
        room.Join(2);
        room.Leave(2);
        
        // then
        Assert.False(room.IsExist(2));
        Assert.Equal(1, room.GetPlayerCount());
    }
}