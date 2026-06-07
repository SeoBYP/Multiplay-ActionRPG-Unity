using Microsoft.Extensions.Logging.Abstractions;
using Script.System.GamePlayAbilitySystem;
using Server.Room;
using Server.Tests.Fakes;
using Shared.Infrastructure.Messages;
using Shared.Infrastructure.Spawn;

namespace Server.Tests.Room;

/// <summary>
/// M4 A 트랙 ③: 던전 클리어(몬스터 전멸) 감지. Room.TryMarkCleared 가 전멸을 최초 1회만 표시하고,
/// RoomManager.PublishDungeonClear 가 참가자/MapId 를 담아 결과 이벤트를 발행한다.
/// </summary>
public class DungeonClearTests
{
    private static readonly GameplayAttributeModifier Lethal =
        new(EGameplayAttribute.Health, -999, EModifierType.Additive);

    private static global::Server.Room.Room NewRoomWithSlimes(int count)
    {
        var room = new global::Server.Room.Room(
            1,
            new List<PlayerInfo> { new() { UserId = 1, Nickname = "A", SpawnIndex = 0 } },
            NullLogger<global::Server.Room.Room>.Instance);

        room.SpawnMonsters(
            new List<MonsterSpawnDef> { new("slime", 0f, 0f, 0f, 0f, count, 0, Array.Empty<PatrolPoint>()) },
            new MapBounds(0f, 0f, 40f, 40f));
        return room;
    }

    [Fact]
    public void 몬스터가_남아있으면_클리어가_아니다()
    {
        var room = NewRoomWithSlimes(2);
        var first = room.GetAllMonsters()[0].InstanceId;

        room.DamageMonster(first, new[] { Lethal }); // 1마리만 처치, 1마리 생존

        Assert.False(room.TryMarkCleared());
    }

    [Fact]
    public void 전멸하면_TryMarkCleared는_최초_1회만_true다()
    {
        var room = NewRoomWithSlimes(2);
        foreach (var m in room.GetAllMonsters())
            room.DamageMonster(m.InstanceId, new[] { Lethal });

        Assert.Empty(room.GetAllMonsters()); // 전멸
        Assert.True(room.TryMarkCleared());   // 최초 발화
        Assert.False(room.TryMarkCleared());  // 재호출은 false(중복 방지)
    }

    [Fact]
    public void 몬스터를_스폰한적_없는_방은_클리어가_아니다()
    {
        var room = new global::Server.Room.Room(
            2,
            new List<PlayerInfo> { new() { UserId = 1, Nickname = "A", SpawnIndex = 0 } },
            NullLogger<global::Server.Room.Room>.Instance);

        // SpawnMonsters 미호출 → _monsters 비어있지만 클리어 아님(빈 방 오판 방지)
        Assert.False(room.TryMarkCleared());
    }

    [Fact]
    public void PublishDungeonClear는_참가자와_MapId를_담아_발행한다()
    {
        var fake = new FakeDungeonResultPublisher();
        var rm = new RoomManager(
            NullLogger<RoomManager>.Instance,
            NullLogger<global::Server.Room.Room>.Instance,
            new FakeRoomLifecyclePublisher(),
            fake,
            new FakeLootPickupPublisher());

        const long roomId = 50;
        var message = new GameStartRequestedMessage
        {
            RoomId = roomId,
            TraceId = "t",
            MapId = MapIds.Dungeon01,
            PlayerInfos = new List<PlayerInfo>
            {
                new() { UserId = 1, Nickname = "A", SpawnIndex = 0 },
                new() { UserId = 2, Nickname = "B", SpawnIndex = 1 },
            }
        };
        var room = rm.CreateRoom(roomId, message.PlayerInfos, message)!;

        rm.PublishDungeonClear(room);

        var published = Assert.Single(fake.Published);
        Assert.Equal(roomId, published.RoomId);
        Assert.Equal(MapIds.Dungeon01, published.MapId);
        Assert.Equal(new long[] { 1, 2 }, published.Participants.OrderBy(x => x).ToArray());
    }
}
