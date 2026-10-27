using Microsoft.Extensions.Logging.Abstractions;
using Server.PacketHandler.Handler;
using Server.Room;
using Server.Tests.Fakes;
using Shared.Infrastructure.Messages;
using Shared.Packet.Packets;

namespace Server.Tests.Room;

/// <summary>S3 회귀 — 이동 검증을 넣은 뒤에도 정상 이동은 C_Move 경로로 그대로 반영돼야 한다.</summary>
public class MoveRegressionTests
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

    private static Task Move(Session session, float x, float y, float z, float rotY = 0f)
        => MovementHandler.HandleMove(session, new C_Move { PosX = x, PosY = y, PosZ = z, RotY = rotY }, CancellationToken.None).AsTask();

    [Fact]
    public async Task 연달아_도착한_정상_이동_패킷은_모두_반영된다()
    {
        var (session, room) = PlayerAtOrigin();

        for (int i = 1; i <= 10; i++)
            await Move(session, i * 0.25f, 0f, 0f);

        var actor = room.Actors.GetMember(UserId)!.Actor;
        Assert.Equal(2.5f, actor.PosX, 0.001f);
    }

    [Fact]
    public async Task 순간이동이_잘린_뒤에도_이어지는_정상_이동은_반영된다()
    {
        // 거부 방식이면 잘린 뒤 서버 위치가 굳는다 — 잘린 위치 기준 정상 이동은 다시 받아들여야 한다.
        var (session, room) = PlayerAtOrigin();
        var actor = room.Actors.GetMember(UserId)!.Actor;

        await Move(session, 500f, 0f, 500f);
        float clampedX = actor.PosX, clampedZ = actor.PosZ;
        await Move(session, clampedX + 0.25f, 0f, clampedZ);

        Assert.Equal(clampedX + 0.25f, actor.PosX, 0.001f);
        Assert.Equal(clampedZ, actor.PosZ, 0.001f);
    }

    [Fact]
    public async Task 높이_변화는_수평_이동_상한에_걸리지_않는다()
    {
        // 점프·낙하 — 상한은 XZ 수평 거리만 본다.
        var (session, room) = PlayerAtOrigin();

        await Move(session, 0f, 30f, 0f);

        Assert.Equal(30f, room.Actors.GetMember(UserId)!.Actor.PosY, 0.001f);
    }

    [Fact]
    public async Task 회전값은_그대로_반영된다()
    {
        var (session, room) = PlayerAtOrigin();

        await Move(session, 0.1f, 0f, 0f, rotY: 270f);

        Assert.Equal(270f, room.Actors.GetMember(UserId)!.Actor.RotY, 0.001f);
    }
}
