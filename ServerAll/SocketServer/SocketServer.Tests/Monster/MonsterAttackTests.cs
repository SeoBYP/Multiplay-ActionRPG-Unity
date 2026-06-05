using Microsoft.Extensions.Logging.Abstractions;
using Shared.Infrastructure.Messages;
using Shared.Infrastructure.Spawn;
using Shared.Packet.Packets;

namespace Server.Tests.Monster;

/// <summary>
/// M3 ⑤b: 몬스터→플레이어 공격. Attack 페이즈 + 쿨다운 경과 시 최근접 플레이어에
/// monster_attack_dmg(S_ApplyEffect)를 발행한다.
/// </summary>
public class MonsterAttackTests
{
    private static global::Server.Room.Room NewRoom()
        => new(1,
            new List<PlayerInfo> { new() { UserId = 100, Nickname = "A", SpawnIndex = 0 } },
            NullLogger<global::Server.Room.Room>.Instance);

    [Fact]
    public void 몬스터가_사거리_안_플레이어를_쿨다운마다_공격한다()
    {
        var room = NewRoom();
        room.InitPlayerState(100, "A", 0, 0.5f, 0f, 0f, 0f); // 플레이어를 몬스터(0,0,0) 사거리 안에
        room.SpawnMonsters(
            new List<MonsterSpawnDef> { new("slime", 0f, 0f, 0f, 0f, 1, 0, Array.Empty<PatrolPoint>()) },
            new MapBounds(0f, 0f, 40f, 40f));

        const long t0 = 1_000_000; // LastAttackAt=0 이므로 첫 틱은 즉시 공격

        var p1 = room.TickMonsters(0.1f, t0);
        var atk1 = Assert.Single(p1.OfType<S_ApplyEffect>());
        Assert.Equal("monster_attack_dmg", atk1.EffectId);
        Assert.Equal(100, atk1.TargetId);
        Assert.Equal(0, atk1.SourceId); // 0 = 몬스터/환경

        // 즉시 다시 틱 → 쿨다운(1500ms) 내라 공격 없음
        var p2 = room.TickMonsters(0.1f, t0 + 100);
        Assert.Empty(p2.OfType<S_ApplyEffect>());

        // 쿨다운 경과 후 → 다시 공격
        var p3 = room.TickMonsters(0.1f, t0 + 2000);
        Assert.Single(p3.OfType<S_ApplyEffect>());
    }

    [Fact]
    public void 플레이어가_aggro밖이면_공격하지_않는다()
    {
        var room = NewRoom();
        room.InitPlayerState(100, "A", 0, 100f, 0f, 0f, 0f); // 멀리(aggro 밖)
        room.SpawnMonsters(
            new List<MonsterSpawnDef> { new("slime", 0f, 0f, 0f, 0f, 1, 0, Array.Empty<PatrolPoint>()) },
            new MapBounds(0f, 0f, 400f, 400f));

        var packets = room.TickMonsters(0.1f, 1_000_000);

        Assert.Empty(packets.OfType<S_ApplyEffect>());   // 공격 없음
        Assert.NotEmpty(packets.OfType<S_MonsterState>()); // 상태 브로드캐스트는 여전히 함
    }
}
