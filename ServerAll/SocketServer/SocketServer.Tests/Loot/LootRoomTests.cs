using Microsoft.Extensions.Logging.Abstractions;
using Server.Player;
using Shared.Infrastructure.Messages;

namespace Server.Tests.Loot;

/// <summary>
/// Room 의 바닥 아이템 보유·줍기 경쟁 중재 검증(서버 권위).
/// SpawnGroundItem(순차 GroundId)·TryPickup(거리·경쟁 1회).
/// </summary>
public class LootRoomTests
{
    private static global::Server.Room.Room NewRoom(long userId)
    {
        var room = new global::Server.Room.Room(
            roomId: 1,
            expectedUserIds: new List<PlayerInfo> { new() { UserId = userId, Nickname = "A", SpawnIndex = 0 } },
            logger: NullLogger<global::Server.Room.Room>.Instance);
        // 플레이어를 원점에 배치(거리 검증 기준).
        room.InitPlayerState(userId, "A", 0, 0f, 0f, 0f, 0f);
        return room;
    }

    [Fact]
    public void SpawnGroundItem은_GroundId를_1부터_순차_발급한다()
    {
        var room = NewRoom(100);

        var a = room.SpawnGroundItem("potion_hp_small", 1, 0f, 0f, 0f);
        var b = room.SpawnGroundItem("gold", 3, 0f, 0f, 0f);

        Assert.Equal(1, a.GroundId);
        Assert.Equal(2, b.GroundId);
        Assert.Equal(2, room.GetAllGroundItems().Count);
    }

    [Fact]
    public void 범위_안_줍기는_성공하고_바닥에서_제거된다()
    {
        var room = NewRoom(100);
        var ground = room.SpawnGroundItem("potion_hp_small", 1, 1f, 0f, 1f); // 원점에서 √2 < 3

        var picked = room.TryPickup(100, ground.GroundId);

        Assert.NotNull(picked);
        Assert.Equal("potion_hp_small", picked!.ItemId);
        Assert.Empty(room.GetAllGroundItems());
    }

    [Fact]
    public void 동시_줍기는_한_명만_성공한다_경쟁중재()
    {
        var room = NewRoom(100);
        var ground = room.SpawnGroundItem("gold", 3, 0f, 0f, 0f);

        var first = room.TryPickup(100, ground.GroundId);
        var second = room.TryPickup(100, ground.GroundId); // 이미 제거됨 = 경쟁 패배

        Assert.NotNull(first);
        Assert.Null(second);
    }

    [Fact]
    public void 범위_밖_줍기는_실패하고_바닥에_남는다()
    {
        var room = NewRoom(100);
        var ground = room.SpawnGroundItem("potion_hp_small", 1, 10f, 0f, 10f); // PickupRange(3) 밖

        var picked = room.TryPickup(100, ground.GroundId);

        Assert.Null(picked);
        Assert.Single(room.GetAllGroundItems());
    }

    [Fact]
    public void 존재하지_않는_GroundId_줍기는_null이다()
    {
        var room = NewRoom(100);
        Assert.Null(room.TryPickup(100, 999));
    }
}
