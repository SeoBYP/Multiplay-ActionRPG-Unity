using Microsoft.Extensions.Logging.Abstractions;
using Server.Room;
using Server.Tests.Fakes;
using Shared.Infrastructure.Messages;

namespace Server.Tests.Room;

/// <summary>
/// 재접속 인수(takeover) — 같은 UserId 가 다시 들어오면 <b>옛 세션을 비우고 그 자리를 넘겨받는다</b>.
///
/// <b>왜 필요한가</b>(실측): 에디터 Play 정지처럼 FIN 없이 사라지면 서버는 그 세션을 바로 죽었다고 보지 못한다.
/// 유휴 타임아웃까지(실측 <b>63초</b>) 세션이 방에 남아 있어 2/2 방은 <b>본인조차</b> "Room is full" 로 거절됐다
/// (실측 로그: `Room 3427 is full. Session 1042 cannot join` × 30, 그 뒤 `Room player timed out — UserId=1`).
/// 재접속 유예(60s)는 그 다음에야 시작되므로, 유예가 있어도 돌아올 방법이 없었다.
///
/// 인수는 <b>세션만</b> 교체한다 — PlayerState(위치·HP)는 보존해야 원래 자리로 복귀한다.
/// </summary>
public class ReconnectTakeoverTests
{
    /// <summary>정원 2명 방(입장 전) — 실제 던전과 같은 2인 Co-op 구성.</summary>
    private static (RoomManager manager, global::Server.Room.Room room) NewFullRoom()
    {
        var manager = new RoomManager(
            NullLogger<RoomManager>.Instance,
            NullLogger<global::Server.Room.Room>.Instance,
            new FakeRoomLifecyclePublisher(),
            new FakeDungeonResultPublisher(),
            new FakeLootPickupPublisher());

        var message = new GameStartRequestedMessage
        {
            RoomId = 1,
            PlayerInfos = new List<PlayerInfo>
            {
                new() { UserId = 100, Nickname = "A", SpawnIndex = 0 },
                new() { UserId = 200, Nickname = "B", SpawnIndex = 1 },
            },
            TraceId = "trace-takeover",
        };

        var room = manager.CreateRoom(message.RoomId, message.PlayerInfos, message)!;
        return (manager, room);
    }

    [Fact]
    public void 같은_유저가_다시_입장하면_옛_세션을_인수하고_정원을_넘지_않는다()
    {
        var (manager, room) = NewFullRoom();
        var a = TestSessionFactory.Create(manager, sessionId: 1, userId: 100);
        var b = TestSessionFactory.Create(manager, sessionId: 2, userId: 200);
        Assert.True(manager.JoinRoom(a, room.RoomId));
        Assert.True(manager.JoinRoom(b, room.RoomId));
        Assert.True(room.IsFull);

        // 100 번이 끊긴 걸 서버가 아직 모르는 상태에서 재접속.
        var aAgain = TestSessionFactory.Create(manager, sessionId: 3, userId: 100);
        Assert.True(manager.JoinRoom(aAgain, room.RoomId),
            "같은 UserId 의 재접속은 옛 세션을 인수해 들어와야 한다(지금은 full 로 거절된다).");

        Assert.Equal(2, room.MemberCount);
        Assert.Null(room.GetSession(1));                 // 옛 세션은 방에서 빠졌다
        Assert.NotNull(room.GetSession(3));
    }

    [Fact]
    public void 인수해도_PlayerState는_보존된다()
    {
        var (manager, room) = NewFullRoom();
        room.InitPlayerState(100, "A", 0, 7f, 0f, -3f, 90f);
        var a = TestSessionFactory.Create(manager, sessionId: 1, userId: 100);
        manager.JoinRoom(a, room.RoomId);

        var aAgain = TestSessionFactory.Create(manager, sessionId: 3, userId: 100);
        Assert.True(manager.JoinRoom(aAgain, room.RoomId));

        var state = room.GetPlayerState(100);
        Assert.NotNull(state);
        Assert.Equal(7f, state!.PosX);   // 끊긴 자리 그대로 복귀
        Assert.Equal(-3f, state.PosZ);
    }

    [Fact]
    public void 다른_유저는_정원이_찼으면_여전히_거절된다()
    {
        var (manager, room) = NewFullRoom();
        var a = TestSessionFactory.Create(manager, sessionId: 1, userId: 100);
        var b = TestSessionFactory.Create(manager, sessionId: 2, userId: 200);
        manager.JoinRoom(a, room.RoomId);
        manager.JoinRoom(b, room.RoomId);

        var stranger = TestSessionFactory.Create(manager, sessionId: 4, userId: 999);
        Assert.False(manager.JoinRoom(stranger, room.RoomId),
            "인수는 같은 UserId 에만 허용된다 — 남의 자리를 뺏으면 안 된다.");
        Assert.Equal(2, room.MemberCount);
    }

    [Fact]
    public void 인수_후_옛_세션이_뒤늦게_정리돼도_새_세션은_남는다()
    {
        // 실제 순서: 재접속(인수)이 먼저 일어나고, 서버가 옛 소켓의 죽음을 <b>60초 뒤</b> 유휴 타임아웃으로 뒤늦게 안다.
        // 그때 옛 세션의 퇴장 처리가 방금 돌아온 사람을 걷어차면 안 된다.
        var (manager, room) = NewFullRoom();
        var a = TestSessionFactory.Create(manager, sessionId: 1, userId: 100);
        var b = TestSessionFactory.Create(manager, sessionId: 2, userId: 200);
        manager.JoinRoom(a, room.RoomId);
        manager.JoinRoom(b, room.RoomId);

        var aAgain = TestSessionFactory.Create(manager, sessionId: 3, userId: 100);
        Assert.True(manager.JoinRoom(aAgain, room.RoomId));

        // 뒤늦은 타임아웃 처리(크래시 경로 = graceful)
        manager.LeaveRoom(a, graceful: true);

        Assert.Equal(2, room.MemberCount);
        Assert.NotNull(room.GetSession(3));
        Assert.NotNull(room.GetSession(2));
    }
}
