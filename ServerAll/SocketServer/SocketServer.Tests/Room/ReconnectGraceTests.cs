using Microsoft.Extensions.Logging.Abstractions;
using Server.Room;
using Server.Tests.Fakes;
using Shared.Infrastructure.Messages;

namespace Server.Tests.Room;

/// <summary>
/// 재접속 유예 창(grace window) 검증 — 크래시/끊김(graceful) 퇴장은 방에 다른 플레이어가 남아 있는 한
/// PlayerState 를 ReconnectGraceMs 동안 보존(재접속 시 복귀)하고, 만료되면 스윕이 영구 퇴장으로 확정한다.
///
/// 대비: 명시 퇴장(C_PlayerLeave, graceful=false)은 즉시 제거 — RoomManagerLeaveRoomTests 가 검증.
/// 배경: 9.4 부채 수정이 "모든 Leave에서 상태 즉시 제거"라 크래시=재접속 불가 회귀를 만들었고, 이를 해소한다.
/// </summary>
public class ReconnectGraceTests
{
    private const long GraceMs = global::Server.Room.Room.ReconnectGraceMs;

    private readonly FakeRoomLifecyclePublisher _publisher = new();
    private readonly RoomManager _roomManager;

    public ReconnectGraceTests()
    {
        _roomManager = new RoomManager(
            NullLogger<RoomManager>.Instance,
            NullLogger<global::Server.Room.Room>.Instance,
            _publisher,
            new FakeDungeonResultPublisher(),
            new FakeLootPickupPublisher());
    }

    private static GameStartRequestedMessage BuildMessage(long roomId, params long[] userIds)
    {
        var playerInfos = new List<PlayerInfo>();
        for (var i = 0; i < userIds.Length; i++)
            playerInfos.Add(new PlayerInfo { UserId = userIds[i], Nickname = $"User{userIds[i]}", SpawnIndex = i });

        return new GameStartRequestedMessage { RoomId = roomId, PlayerInfos = playerInfos, TraceId = "trace-test" };
    }

    private global::Server.Room.Room CreateJoinedRoom(long roomId, out Session[] sessions, params long[] userIds)
    {
        var message = BuildMessage(roomId, userIds);
        _roomManager.CreateRoom(roomId, message.PlayerInfos, message);

        sessions = new Session[userIds.Length];
        for (var i = 0; i < userIds.Length; i++)
        {
            sessions[i] = TestSessionFactory.Create(_roomManager, sessionId: (ulong)(i + 1), userId: userIds[i]);
            _roomManager.JoinRoom(sessions[i], roomId);
        }

        return _roomManager.GetRoom(roomId)!;
    }

    [Fact]
    public void graceful_퇴장은_남은_플레이어_있으면_상태_보존하고_발행_보류한다()
    {
        var room = CreateJoinedRoom(1, out var sessions, 100, 200);

        var left = _roomManager.LeaveRoom(sessions[0], graceful: true);

        Assert.True(left);
        Assert.NotNull(_roomManager.GetRoom(1));               // 방 유지(200 남음)
        var state = room.GetPlayerState(100);
        Assert.NotNull(state);                                  // 상태 보존(즉시 제거 X)
        Assert.NotNull(state!.DisconnectedAtMs);               // 끊김 마킹됨
        Assert.Empty(_publisher.Published);                    // 발행 보류(아직 퇴장 확정 아님)
    }

    [Fact]
    public void graceful_퇴장후_재접속하면_보존_상태로_복귀한다()
    {
        var room = CreateJoinedRoom(2, out var sessions, 100, 200);
        var before = room.GetPlayerState(100)!;
        before.PosX = 12.5f; before.PosZ = -7.5f;               // 끊기기 전 위치

        _roomManager.LeaveRoom(sessions[0], graceful: true);   // 크래시
        Assert.NotNull(room.GetPlayerState(100)!.DisconnectedAtMs);

        var reconnected = room.MarkJoined(100);                // 재접속(유예 내)

        Assert.True(reconnected);
        var state = room.GetPlayerState(100)!;
        Assert.Null(state.DisconnectedAtMs);                   // 마킹 해제 = 다시 활성
        Assert.Equal(12.5f, state.PosX);                       // 보존된 위치로 복귀
        Assert.Equal(-7.5f, state.PosZ);
    }

    [Fact]
    public void 유예_만료_스윕은_상태_제거하고_영구퇴장_발행한다()
    {
        var room = CreateJoinedRoom(3, out var sessions, 100, 200);
        _roomManager.LeaveRoom(sessions[0], graceful: true);

        long afterGrace = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + GraceMs + 1_000;
        _roomManager.SweepDisconnectedPlayers(afterGrace);

        Assert.Null(room.GetPlayerState(100));                 // 만료 → 제거
        Assert.NotNull(room.GetPlayerState(200));              // 접속 중인 200은 유지
        Assert.Single(_publisher.Published);                   // 만료 시점에 1회 발행
        Assert.Equal(100, _publisher.Published[0].UserId);
        Assert.False(_publisher.Published[0].RoomEmptied);     // 200 남음
    }

    [Fact]
    public void 유예_내_스윕은_아무것도_정리하지_않는다()
    {
        var room = CreateJoinedRoom(4, out var sessions, 100, 200);
        _roomManager.LeaveRoom(sessions[0], graceful: true);

        long withinGrace = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();   // 막 끊김 — 유예 내
        _roomManager.SweepDisconnectedPlayers(withinGrace);

        Assert.NotNull(room.GetPlayerState(100));              // 아직 보존
        Assert.Empty(_publisher.Published);
    }

    [Fact]
    public void graceful이라도_마지막_플레이어_퇴장이면_즉시_방제거하고_RoomEmptied_true로_발행한다()
    {
        CreateJoinedRoom(5, out var sessions, 100);            // 1인 방

        var left = _roomManager.LeaveRoom(sessions[0], graceful: true);

        Assert.True(left);
        Assert.Null(_roomManager.GetRoom(5));                  // 빈 방은 유예 없이 즉시 제거
        Assert.Single(_publisher.Published);
        Assert.Equal(100, _publisher.Published[0].UserId);
        Assert.True(_publisher.Published[0].RoomEmptied);
    }

    [Fact]
    public void 전원_graceful_퇴장시_보류됐던_플레이어도_방제거때_함께_발행된다()
    {
        var room = CreateJoinedRoom(6, out var sessions, 100, 200);

        _roomManager.LeaveRoom(sessions[0], graceful: true);  // 100 크래시 — 보류(방에 200 남음)
        Assert.Empty(_publisher.Published);

        _roomManager.LeaveRoom(sessions[1], graceful: true);  // 200 크래시 — 방 빔 → 즉시 확정

        Assert.Null(_roomManager.GetRoom(6));                 // 방 제거
        // 200(마지막 퇴장) + 보류됐던 100 둘 다 association 정리 발행(누락 방지).
        Assert.Equal(2, _publisher.Published.Count);
        Assert.Contains(_publisher.Published, m => m.UserId == 100 && m.RoomEmptied);
        Assert.Contains(_publisher.Published, m => m.UserId == 200 && m.RoomEmptied);
    }
}
