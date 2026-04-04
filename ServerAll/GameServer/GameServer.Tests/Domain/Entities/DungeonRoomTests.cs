using GameServer.Domain.Entities;

namespace GameServer.Tests.Domain.Entities;

public class DungeonRoomTests
{
    [Fact]
    public void Create_는_유효한_파라미터로_던전룸을_생성한다()
    {
        var room = DungeonRoom.Create("testRoom", 1, 4);

        Assert.Equal("testRoom", room.RoomName);
        Assert.Equal(1, room.HostUserId);
        Assert.Equal(4, room.MaxPlayers);
        Assert.Equal(RoomStatus.Waiting, room.Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_는_roomName이_비어있으면_예외를_던진다(string roomName)
    {
        Assert.Throws<ArgumentException>(() => DungeonRoom.Create(roomName, 1, 4));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_는_hostUserId가_0이하면_예외를_던진다(long hostId)
    {
        Assert.Throws<ArgumentException>(() => DungeonRoom.Create("testRoom", hostId, 4));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    public void Create_는_maxPlayers가_2미만이면_예외를_던진다(int maxPlayers)
    {
        Assert.Throws<ArgumentException>(() => DungeonRoom.Create("testRoom", 1, maxPlayers));
    }

    [Fact]
    public void UpdateRoomSettings_은_방장이면_방이름과_최대인원을_변경할_수_있다()
    {
        var room = DungeonRoom.Create("testRoom", 1, 4);

        room.UpdateRoomSettings(1, currentPlayerCount: 2, roomName: "newRoom", maxPlayers: 5);

        Assert.Equal("newRoom", room.RoomName);
        Assert.Equal(5, room.MaxPlayers);
    }

    [Fact]
    public void UpdateRoomSettings_은_방장이_아니면_예외를_던진다()
    {
        var room = DungeonRoom.Create("testRoom", 1, 4);

        Assert.Throws<UnauthorizedAccessException>(() =>
            room.UpdateRoomSettings(2, currentPlayerCount: 1, roomName: "newRoom"));
    }

    [Fact]
    public void UpdateRoomSettings_은_현재_플레이어보다_작게_변경하면_실패한다()
    {
        var room = DungeonRoom.Create("testRoom", 1, 4);

        Assert.Throws<InvalidOperationException>(() =>
            room.UpdateRoomSettings(1, currentPlayerCount: 3, maxPlayers: 2));
    }

    [Fact]
    public void StartGame_은_방장이면_게임을_시작할_수_있다()
    {
        var room = DungeonRoom.Create("testRoom", 1, 4);

        room.StartGame(1, playerCount: 2);

        Assert.Equal(RoomStatus.Starting, room.Status);
    }

    [Fact]
    public void StartGame_은_플레이어가_2명_미만이면_예외를_던진다()
    {
        var room = DungeonRoom.Create("testRoom", 1, 4);

        Assert.Throws<InvalidOperationException>(() => room.StartGame(1, playerCount: 1));
    }

    [Fact]
    public void StartGame_은_방장이_아니면_예외를_던진다()
    {
        var room = DungeonRoom.Create("testRoom", 1, 4);

        Assert.Throws<UnauthorizedAccessException>(() => room.StartGame(2, playerCount: 2));
    }

    [Fact]
    public void ChangeHost_는_새로운_방장으로_변경한다()
    {
        var room = DungeonRoom.Create("testRoom", 1, 4);

        room.ChangeHost(2);

        Assert.Equal(2, room.HostUserId);
        Assert.True(room.IsHost(2));
    }

    [Fact]
    public void Close_는_방_상태를_Closed로_변경한다()
    {
        var room = DungeonRoom.Create("testRoom", 1, 4);

        room.Close();

        Assert.Equal(RoomStatus.Closed, room.Status);
    }

    [Fact]
    public void MarkGameSessionReady_는_Starting에서_Playing으로_변경한다()
    {
        var room = DungeonRoom.Create("testRoom", 1, 4);
        room.StartGame(1, playerCount: 2);

        room.MarkGameSessionReady();

        Assert.Equal(RoomStatus.Playing, room.Status);
    }
}
