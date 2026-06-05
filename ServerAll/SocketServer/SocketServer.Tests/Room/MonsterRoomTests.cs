using Microsoft.Extensions.Logging.Abstractions;
using Server.Room;
using Server.Tests.Fakes;
using Shared.Infrastructure.Messages;
using Shared.Infrastructure.Spawn;

namespace Server.Tests.Room;

/// <summary>
/// M3 증분③: Room 의 서버 권위 몬스터 스폰. CreateRoom 이 맵 레이아웃의 몬스터를 스폰하고,
/// SpawnMonsters 가 count 만큼 고유 InstanceId 로 생성하며 경계를 보관하는지 검증.
/// </summary>
public class MonsterRoomTests
{
    private static RoomManager NewRoomManager() => new(
        NullLogger<RoomManager>.Instance,
        NullLogger<global::Server.Room.Room>.Instance,
        new FakeRoomLifecyclePublisher(),
        new FakeDungeonResultPublisher());

    [Fact]
    public void CreateRoom은_맵의_몬스터를_스폰한다()
    {
        var rm = NewRoomManager();
        const long roomId = 30;
        var message = new GameStartRequestedMessage
        {
            RoomId = roomId,
            TraceId = "t",
            MapId = MapIds.Dungeon01,
            PlayerInfos = new List<PlayerInfo> { new() { UserId = 1, Nickname = "A", SpawnIndex = 0 } }
        };

        var room = rm.CreateRoom(roomId, message.PlayerInfos, message);
        Assert.NotNull(room);

        var slime = Assert.Single(room!.GetAllMonsters());
        Assert.Equal("slime", slime.MonsterId);
        Assert.Equal(30, slime.MaxHp);
        Assert.Equal(30, slime.Hp);
        Assert.True(slime.InstanceId > 0);
        Assert.Equal(4, slime.Patrol.Count);
        Assert.Equal(6f, slime.PosX);
        Assert.Equal(6f, slime.PosZ);
        Assert.False(slime.IsDead);
    }

    [Fact]
    public void SpawnMonsters는_count만큼_고유_InstanceId로_생성하고_경계를_보관한다()
    {
        var room = new global::Server.Room.Room(
            99,
            new List<PlayerInfo> { new() { UserId = 1, Nickname = "A", SpawnIndex = 0 } },
            NullLogger<global::Server.Room.Room>.Instance);

        var bounds = new MapBounds(0f, 0f, 40f, 40f);
        var defs = new List<MonsterSpawnDef>
        {
            new("slime", 0f, 0f, 0f, 0f, 3, 0, Array.Empty<PatrolPoint>())
        };

        room.SpawnMonsters(defs, bounds);

        var monsters = room.GetAllMonsters();
        Assert.Equal(3, monsters.Count);
        Assert.Equal(3, monsters.Select(m => m.InstanceId).Distinct().Count()); // 모두 고유
        Assert.Equal(bounds, room.Bounds);
    }
}
