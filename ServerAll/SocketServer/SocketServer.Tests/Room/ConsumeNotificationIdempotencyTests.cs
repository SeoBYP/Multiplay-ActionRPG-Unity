using Microsoft.Extensions.Logging.Abstractions;
using Shared.Infrastructure.Messages;

namespace Server.Tests.Room;

/// <summary>
/// 소비 통지(PlayerConsumed) 중복 차단.
///
/// 전달 의미가 at-least-once 가 되면서 같은 통지가 다시 올 수 있게 됐다.
/// 회복 적용(`ApplyPlayerEffect(+heal)`)은 비멱등이라, 막지 않으면 **포션 하나로 두 번 회복**된다.
/// 차단 수명은 방에 묶는다 — 회복 대상이 방의 인메모리 상태라 방이 사라지면 중복 걱정도 사라진다.
/// </summary>
public class ConsumeNotificationIdempotencyTests
{
    private static global::Server.Room.Room NewRoom()
    {
        var players = new List<PlayerInfo> { new() { UserId = 100, Nickname = "P100", SpawnIndex = 0 } };
        return new global::Server.Room.Room(1, players, NullLogger<global::Server.Room.Room>.Instance);
    }

    [Fact]
    public void 같은_ConsumeId_는_한_번만_받아들인다()
    {
        var room = NewRoom();

        Assert.True(room.TryMarkConsumeHandled("consume-1"));
        Assert.False(room.TryMarkConsumeHandled("consume-1"));
        Assert.False(room.TryMarkConsumeHandled("consume-1"));
    }

    [Fact]
    public void 다른_ConsumeId_는_각각_받아들인다()
    {
        var room = NewRoom();

        Assert.True(room.TryMarkConsumeHandled("consume-1"));
        Assert.True(room.TryMarkConsumeHandled("consume-2"));
    }

    [Fact]
    public void 방이_다르면_같은_ConsumeId_라도_각자_판단한다()
    {
        var a = NewRoom();
        var b = NewRoom();

        Assert.True(a.TryMarkConsumeHandled("consume-1"));
        Assert.True(b.TryMarkConsumeHandled("consume-1"));
    }

    [Fact]
    public void ConsumeId_가_비어있으면_차단하지_않는다()
    {
        var room = NewRoom();

        // 구 메시지(필드 추가 전 발행분)는 멱등키가 없다 — 차단해서 회복을 잃는 쪽이 더 나쁘다.
        Assert.True(room.TryMarkConsumeHandled(""));
        Assert.True(room.TryMarkConsumeHandled(""));
    }
}
