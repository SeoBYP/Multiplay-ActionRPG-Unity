using Script.System.GamePlayAbilitySystem;
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
        new FakeDungeonResultPublisher(),
        new FakeLootPickupPublisher());

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

        // 임베디드 dungeon_01 레이아웃(진실원)이 그대로 스폰됐는지 검증 — 좌표/patrol 하드코딩 대신
        // 레이아웃에서 기대값을 도출해 던전 재기획에도 견고하게 유지한다.
        var layout = SpawnLayoutTable.Get(MapIds.Dungeon01);
        var monsters = room!.Actors.Monsters();

        Assert.Equal(layout.Monsters.Sum(m => Math.Max(1, m.Count)), monsters.Count); // count 합 = 총 마리수
        Assert.All(monsters, m => Assert.True(m.InstanceId > 0));
        Assert.All(monsters, m => Assert.False(m.Gas.IsDead));

        // 레이아웃 첫 정의(초입 = vampire_bat)가 그 위치에 정확히 1마리, 카탈로그 스탯으로 스폰.
        var firstDef = layout.Monsters[0];
        var spawned = Assert.Single(monsters, m => m.PosX == firstDef.X && m.PosZ == firstDef.Z);
        Assert.Equal(firstDef.MonsterId, spawned.MonsterId);
        Assert.Equal(firstDef.Patrol.Count, spawned.Patrol.Count);
        Assert.Equal(spawned.Gas.Max(EGameplayAttribute.Health), spawned.Gas[EGameplayAttribute.Health]); // 스폰 시 풀피
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
            new("creepy_demon", 0f, 0f, 0f, 0f, 3, 0, Array.Empty<PatrolPoint>())
        };

        room.SpawnMonsters(defs, bounds);

        var monsters = room.Actors.Monsters();
        Assert.Equal(3, monsters.Count);
        Assert.Equal(3, monsters.Select(m => m.InstanceId).Distinct().Count()); // 모두 고유
        Assert.Equal(bounds, room.Bounds);
    }
}
