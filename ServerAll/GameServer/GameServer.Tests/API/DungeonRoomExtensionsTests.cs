using GameServer.API.Extension;
using GameServer.Domain.Entities;
using UserEntity = GameServer.Domain.Entities.User.User;

namespace GameServer.Tests.API;

/// <summary>
/// DungeonRoom → RoomInfo(gRPC) 매핑에서 던전 메타(MapId, 4.3)가 누락 없이 실리는지 검증.
/// 배치 조회 동기 오버로드(추가 I/O 0)를 대상으로 한다.
/// </summary>
public class DungeonRoomExtensionsTests
{
    [Fact]
    public void ToRoomInfo_는_방의_MapId를_담는다()
    {
        var room = DungeonRoom.FromRedis(
            roomId: 1, roomName: "r", hostUserId: 1, maxPlayers: 4,
            mapId: "dungeon_01", status: RoomStatus.Waiting, createdAt: DateTime.UnixEpoch);

        var info = room.ToRoomInfo(new List<UserEntity>());

        Assert.Equal("dungeon_01", info.MapId);
    }
}
