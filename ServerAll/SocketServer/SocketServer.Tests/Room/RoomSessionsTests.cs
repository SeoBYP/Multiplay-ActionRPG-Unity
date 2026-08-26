using Microsoft.Extensions.Logging.Abstractions;
using Server.Room;
using Server.Tests.Fakes;

namespace Server.Tests.Room;

/// <summary>
/// 방의 연결 집합 — 정원·중복·인수(takeover) 조회.
///
/// <para><b>참가자(RoomMember)와 수명이 다르다는 것</b>이 이 타입이 따로 있는 이유다.
/// 세션은 끊기면 즉시 사라지지만 참가자·액터는 재접속 유예 동안 살아 있다.
/// 그래서 여기서 세션을 지워도 액터는 건드리지 않는다(그 조율은 Room 의 일).</para>
/// </summary>
public class RoomSessionsTests
{
    private static RoomSessions NewSessions(int maxMembers = 2)
        => new(roomId: 1, maxMembers, NullLogger.Instance);

    private static Session NewSession(ulong sessionId, long userId)
        => TestSessionFactory.Create(roomManager: null!, sessionId, userId);

    [Fact]
    public void 세션을_등록하면_조회된다()
    {
        var sessions = NewSessions();
        var session = NewSession(1, userId: 100);

        Assert.True(sessions.Add(session));

        Assert.Equal(1, sessions.Count);
        Assert.Same(session, sessions.Get(1));
        Assert.Null(sessions.Get(999));
    }

    [Fact]
    public void 정원을_넘으면_거절된다()
    {
        var sessions = NewSessions(maxMembers: 2);
        Assert.True(sessions.Add(NewSession(1, 100)));
        Assert.True(sessions.Add(NewSession(2, 200)));
        Assert.True(sessions.IsFull);

        Assert.False(sessions.Add(NewSession(3, 300)));
        Assert.Equal(2, sessions.Count);
    }

    [Fact]
    public void 같은_SessionId는_중복_등록되지_않는다()
    {
        var sessions = NewSessions();
        Assert.True(sessions.Add(NewSession(1, 100)));

        Assert.False(sessions.Add(NewSession(1, 100)));
        Assert.Equal(1, sessions.Count);
    }

    [Fact]
    public void 제거하면_그_UserId를_돌려준다()
    {
        var sessions = NewSessions();
        sessions.Add(NewSession(1, userId: 100));

        Assert.Equal(100L, sessions.Remove(1));
        Assert.Equal(0, sessions.Count);
        Assert.Null(sessions.Remove(1)); // 없던 세션은 null
    }

    [Fact]
    public void 같은_UserId의_다른_세션을_찾는다_재접속_인수()
    {
        // 크래시 후 재접속: 옛 세션이 아직 방에 남아 있어 정원을 막는다 → 새 세션이 그 자리를 인수한다.
        var sessions = NewSessions();
        sessions.Add(NewSession(1, userId: 100)); // 옛 세션
        sessions.Add(NewSession(2, userId: 200));

        var stale = sessions.FindByUserId(userId: 100, exceptSessionId: 3);

        Assert.NotNull(stale);
        Assert.Equal(1UL, stale!.SessionId);
    }

    [Fact]
    public void 자기_자신은_인수_대상에서_제외된다()
    {
        var sessions = NewSessions();
        sessions.Add(NewSession(1, userId: 100));

        Assert.Null(sessions.FindByUserId(userId: 100, exceptSessionId: 1));
    }

    [Fact]
    public void 미인증_세션은_인수_대상이_아니다()
    {
        // UserId 0 = C_PlayerJoin 전. 남의 자리를 뺏지 않는다.
        var sessions = NewSessions();
        sessions.Add(NewSession(1, userId: 0));

        Assert.Null(sessions.FindByUserId(userId: 0, exceptSessionId: 99));
    }

    [Fact]
    public void 동시_입장에도_정원을_넘지_않는다()
    {
        var sessions = NewSessions(maxMembers: 2);

        int admitted = 0;
        Parallel.For(0, 64, i =>
        {
            if (sessions.Add(NewSession((ulong)(i + 1), 100 + i)))
                Interlocked.Increment(ref admitted);
        });

        Assert.Equal(2, admitted);
        Assert.Equal(2, sessions.Count);
    }
}
