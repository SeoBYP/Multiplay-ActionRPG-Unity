using Microsoft.Extensions.Logging.Abstractions;
using Server.PacketHandler.Handler;
using Shared.Infrastructure.Messages;

namespace Server.Tests.Combat;

/// <summary>
/// EF-2d 테스트 등급 전투: 공격 → Effect 부여 패킷 구성 + 서버 권위 InstanceId 발급 검증.
/// </summary>
public class CombatHandlerTests
{
    [Fact]
    public void BuildAttackEffect는_공격자_대상_효과를_매핑한다()
    {
        var packet = CombatHandler.BuildAttackEffect(attackerId: 200, targetId: 100, instanceId: 5, startTick: 999);

        Assert.Equal(5, packet.InstanceId);
        Assert.Equal(CombatHandler.TestDebuffEffectId, packet.EffectId);
        Assert.Equal(100, packet.TargetId);
        Assert.Equal(200, packet.SourceId);
        Assert.Equal(999, packet.StartTick);
        Assert.Equal(1, packet.Stacks);
    }

    [Fact]
    public void Room_NextEffectInstanceId는_1부터_단조증가한다()
    {
        var room = new global::Server.Room.Room(
            roomId: 1,
            expectedUserIds: new List<PlayerInfo> { new() { UserId = 100, Nickname = "A", SpawnIndex = 0 } },
            logger: NullLogger<global::Server.Room.Room>.Instance);

        Assert.Equal(1, room.NextEffectInstanceId());
        Assert.Equal(2, room.NextEffectInstanceId());
        Assert.Equal(3, room.NextEffectInstanceId());
    }
}
