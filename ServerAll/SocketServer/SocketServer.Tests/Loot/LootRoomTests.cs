using Microsoft.Extensions.Logging.Abstractions;
using Server.Actors;
using Server.Loot;
using Shared.Infrastructure.Messages;

namespace Server.Tests.Loot;

/// <summary>
/// 방 안에서의 바닥 아이템 — <b>저장소(GroundItemStore)와 액터(위치)의 합류</b>를 검증한다.
///
/// <para>저장소 단독 규칙(순차 GroundId·거리·경쟁 중재)은 <c>GroundItemStoreTests</c> 가 이미 고정한다.
/// 여기서 확인하는 것은 그 위 한 겹 — <b>줍기 기준 위치가 시전자 액터에서 온다</b>는 조립이다.
/// 핸들러(LootHandler)가 하는 것과 같은 조립을 그대로 재현한다.</para>
/// </summary>
public class LootRoomTests
{
    private static global::Server.Room.Room NewRoom(long userId)
    {
        var room = new global::Server.Room.Room(
            roomId: 1,
            participants: new List<PlayerInfo> { new() { UserId = userId, Nickname = "A", SpawnIndex = 0 } },
            logger: NullLogger<global::Server.Room.Room>.Instance);
        // 플레이어를 원점에 배치(거리 검증 기준).
        room.AddPlayer(userId, "A", 0, 0f, 0f, 0f, 0f);
        return room;
    }

    /// <summary>LootHandler 와 같은 조립: 시전자 액터의 위치를 스냅샷해 저장소에 넘긴다.</summary>
    private static GroundItem? Pickup(global::Server.Room.Room room, long userId, int groundId)
    {
        var picker = room.Actors.GetMember(userId)?.Actor;
        return picker is null ? null : room.Loot.TryPickup(picker.PosX, picker.PosZ, groundId);
    }

    [Fact]
    public void SpawnGroundItem은_GroundId를_1부터_순차_발급한다()
    {
        var room = NewRoom(100);

        var a = room.Loot.Spawn(1001, 1, 0f, 0f, 0f);
        var b = room.Loot.Spawn(3001, 3, 0f, 0f, 0f);

        Assert.Equal(1, a.GroundId);
        Assert.Equal(2, b.GroundId);
        Assert.Equal(2, room.Loot.All().Count);
    }

    [Fact]
    public void 범위_안_줍기는_성공하고_바닥에서_제거된다()
    {
        var room = NewRoom(100);
        var ground = room.Loot.Spawn(1001, 1, 1f, 0f, 1f); // 원점에서 √2 < 3

        var picked = Pickup(room, 100, ground.GroundId);

        Assert.NotNull(picked);
        Assert.Equal(1001, picked!.ItemId);
        Assert.Empty(room.Loot.All());
    }

    [Fact]
    public void 동시_줍기는_한_명만_성공한다_경쟁중재()
    {
        var room = NewRoom(100);
        var ground = room.Loot.Spawn(3001, 3, 0f, 0f, 0f);

        var first = Pickup(room, 100, ground.GroundId);
        var second = Pickup(room, 100, ground.GroundId); // 이미 제거됨 = 경쟁 패배

        Assert.NotNull(first);
        Assert.Null(second);
    }

    [Fact]
    public void 범위_밖_줍기는_실패하고_바닥에_남는다()
    {
        var room = NewRoom(100);
        var ground = room.Loot.Spawn(1001, 1, 10f, 0f, 10f); // PickupRange(3) 밖

        var picked = Pickup(room, 100, ground.GroundId);

        Assert.Null(picked);
        Assert.Single(room.Loot.All());
    }

    [Fact]
    public void 존재하지_않는_GroundId_줍기는_null이다()
    {
        var room = NewRoom(100);
        Assert.Null(Pickup(room, 100, 999));
    }
}
