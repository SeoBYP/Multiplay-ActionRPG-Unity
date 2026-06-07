using Microsoft.Extensions.Logging.Abstractions;
using Server.Room;
using Server.Tests.Fakes;
using Shared.Infrastructure.Messages;

namespace Server.Tests.Room;

/// <summary>
/// RoomManager.LeaveRoom 의 방 생명주기 동작 검증.
///
/// 핵심 규칙:
///   - 퇴장마다 PlayerLeftRoomMessage 를 발행한다(RoomEmptied로 빈 방 여부 표시).
///   - 남은 플레이어가 있으면 방을 유지하고 발행하지 않는다.
///   - 같은 세션을 두 번 LeaveRoom 해도 멱등하다 (발행 1회).
/// </summary>
public class RoomManagerLeaveRoomTests
{
    private readonly FakeRoomLifecyclePublisher _publisher = new();
    private readonly RoomManager _roomManager;

    public RoomManagerLeaveRoomTests()
    {
        _roomManager = new RoomManager(
            NullLogger<RoomManager>.Instance,
            NullLogger<global::Server.Room.Room>.Instance,
            _publisher,
            new FakeDungeonResultPublisher());
    }

    private static GameStartRequestedMessage BuildMessage(long roomId, params long[] userIds)
    {
        var playerInfos = new List<PlayerInfo>();
        for (var i = 0; i < userIds.Length; i++)
        {
            playerInfos.Add(new PlayerInfo
            {
                UserId = userIds[i],
                Nickname = $"User{userIds[i]}",
                SpawnIndex = i
            });
        }

        return new GameStartRequestedMessage
        {
            RoomId = roomId,
            PlayerInfos = playerInfos,
            TraceId = "trace-test"
        };
    }

    [Fact]
    public void 마지막_플레이어_퇴장시_방_제거_및_RoomEmptied_true로_발행()
    {
        const long roomId = 1;
        var message = BuildMessage(roomId, 100);
        _roomManager.CreateRoom(roomId, message.PlayerInfos, message);

        var session = TestSessionFactory.Create(_roomManager, sessionId: 1, userId: 100);
        Assert.True(_roomManager.JoinRoom(session, roomId));

        var left = _roomManager.LeaveRoom(session);

        Assert.True(left);
        Assert.Null(_roomManager.GetRoom(roomId));
        Assert.Equal(0, _roomManager.RoomCount);
        Assert.Single(_publisher.Published);
        Assert.Equal(roomId, _publisher.Published[0].RoomId);
        Assert.Equal(100, _publisher.Published[0].UserId);
        Assert.True(_publisher.Published[0].RoomEmptied); // 마지막 퇴장 → 빈 방
    }

    [Fact]
    public void 남은_플레이어가_있으면_방_유지하고_RoomEmptied_false로_발행()
    {
        const long roomId = 2;
        var message = BuildMessage(roomId, 100, 200);
        _roomManager.CreateRoom(roomId, message.PlayerInfos, message);

        var session1 = TestSessionFactory.Create(_roomManager, sessionId: 1, userId: 100);
        var session2 = TestSessionFactory.Create(_roomManager, sessionId: 2, userId: 200);
        _roomManager.JoinRoom(session1, roomId);
        _roomManager.JoinRoom(session2, roomId);

        var left = _roomManager.LeaveRoom(session1);

        Assert.True(left);
        Assert.NotNull(_roomManager.GetRoom(roomId));
        Assert.Equal(1, _roomManager.GetRoom(roomId)!.MemberCount);
        // 부분 퇴장도 반드시 발행해야 GameServer가 떠난 유저 association을 제거한다.
        Assert.Single(_publisher.Published);
        Assert.Equal(100, _publisher.Published[0].UserId);
        Assert.False(_publisher.Published[0].RoomEmptied); // 남은 인원 있음
    }

    [Fact]
    public void 같은_세션_두_번_퇴장해도_멱등하다()
    {
        const long roomId = 3;
        var message = BuildMessage(roomId, 100);
        _roomManager.CreateRoom(roomId, message.PlayerInfos, message);

        var session = TestSessionFactory.Create(_roomManager, sessionId: 1, userId: 100);
        _roomManager.JoinRoom(session, roomId);

        var first = _roomManager.LeaveRoom(session);
        var second = _roomManager.LeaveRoom(session);

        Assert.True(first);
        Assert.False(second); // 이미 제거됨 — TryRemove 실패
        Assert.Single(_publisher.Published); // 발행은 1회만
    }

    [Fact]
    public void 퇴장한_플레이어의_PlayerState는_정리된다()
    {
        const long roomId = 4;
        var message = BuildMessage(roomId, 100, 200);
        _roomManager.CreateRoom(roomId, message.PlayerInfos, message);

        var session1 = TestSessionFactory.Create(_roomManager, sessionId: 1, userId: 100);
        var session2 = TestSessionFactory.Create(_roomManager, sessionId: 2, userId: 200);
        _roomManager.JoinRoom(session1, roomId);
        _roomManager.JoinRoom(session2, roomId);

        // 퇴장 전엔 두 플레이어 모두 상태 존재
        Assert.NotNull(_roomManager.GetRoom(roomId)!.GetPlayerState(100));
        Assert.NotNull(_roomManager.GetRoom(roomId)!.GetPlayerState(200));

        _roomManager.LeaveRoom(session1);

        var room = _roomManager.GetRoom(roomId)!;
        // 떠난 100은 정리되고(유령 잔류 X), 남은 200만 유지
        Assert.Null(room.GetPlayerState(100));
        Assert.NotNull(room.GetPlayerState(200));
        Assert.DoesNotContain(room.GetAllPlayerStates(), s => s.UserId == 100);
    }

    [Fact]
    public void 방에_속하지_않은_세션_퇴장은_false_반환()
    {
        var session = TestSessionFactory.Create(_roomManager, sessionId: 99, userId: 999);

        var left = _roomManager.LeaveRoom(session);

        Assert.False(left);
        Assert.Empty(_publisher.Published);
    }
}
