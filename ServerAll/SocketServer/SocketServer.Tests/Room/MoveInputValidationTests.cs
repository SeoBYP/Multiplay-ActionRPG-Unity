using Microsoft.Extensions.Logging.Abstractions;
using Server.Loot;
using Server.PacketHandler.Handler;
using Server.Room;
using Server.Tests.Fakes;
using Shared.Infrastructure.Messages;
using Shared.Packet.Packets;

namespace Server.Tests.Room;

/// <summary>
/// S3 — C_Move 는 지금 아무 검증 없이 액터 위치가 된다.
///
/// 위치는 줍기·부활·몬스터 hitbox 판정의 입력이라, 여기가 뚫리면 거리 검사 전부가 뚫린다.
/// 특히 NaN 은 모든 비교(<c>NaN &gt; r²</c>)가 false 라 "범위 밖" 판정을 <b>항상 통과</b>한다.
/// </summary>
public class MoveInputValidationTests
{
    private const long UserId = 100;

    private static (Session session, global::Server.Room.Room room) PlayerAtOrigin()
    {
        var manager = new RoomManager(
            NullLogger<RoomManager>.Instance,
            NullLogger<global::Server.Room.Room>.Instance,
            new FakeRoomLifecyclePublisher(),
            new FakeDungeonResultPublisher(),
            new FakeLootPickupPublisher());

        var room = new global::Server.Room.Room(
            1,
            new List<PlayerInfo> { new() { UserId = UserId, Nickname = "A", SpawnIndex = 0 } },
            NullLogger<global::Server.Room.Room>.Instance);
        room.AddPlayer(UserId, "A", 0, 0f, 0f, 0f, 0f);
        room.MarkJoined(UserId);

        var session = TestSessionFactory.Create(manager, sessionId: 1, userId: UserId);
        session.Room = room;
        return (session, room);
    }

    [Theory]
    [InlineData(float.NaN, 0f)]
    [InlineData(0f, float.NaN)]
    [InlineData(float.PositiveInfinity, 0f)]
    [InlineData(0f, float.NegativeInfinity)]
    public async Task 유한하지_않은_좌표는_위치로_반영되지_않는다(float x, float z)
    {
        var (session, room) = PlayerAtOrigin();

        await MovementHandler.HandleMove(session, new C_Move { PosX = x, PosY = 0f, PosZ = z }, CancellationToken.None);

        var actor = room.Actors.GetMember(UserId)!.Actor;
        Assert.True(float.IsFinite(actor.PosX) && float.IsFinite(actor.PosZ),
            $"서버 위치가 ({actor.PosX}, {actor.PosZ}) 가 됐다 — 이후 모든 거리 검사가 무력화된다.");
    }

    [Fact]
    public async Task 패킷_한_번으로_맵_반대편까지_순간이동할_수_없다()
    {
        var (session, room) = PlayerAtOrigin();

        await MovementHandler.HandleMove(session, new C_Move { PosX = 500f, PosY = 0f, PosZ = 500f }, CancellationToken.None);

        var actor = room.Actors.GetMember(UserId)!.Actor;
        float moved = MathF.Sqrt(actor.PosX * actor.PosX + actor.PosZ * actor.PosZ);
        Assert.True(moved < 50f, $"한 패킷에 {moved:F0}m 이동이 그대로 받아들여졌다.");
    }

    [Fact]
    public void 심층방어_NaN_위치로는_바닥_아이템을_주울_수_없다()
    {
        var store = new GroundItemStore();
        var item = store.Spawn(itemId: 1001, qty: 1, x: 30f, y: 0f, z: 30f); // 30m 밖

        var picked = store.TryPickup(pickerX: float.NaN, pickerZ: float.NaN, groundId: item.GroundId);

        Assert.Null(picked);
    }
}
