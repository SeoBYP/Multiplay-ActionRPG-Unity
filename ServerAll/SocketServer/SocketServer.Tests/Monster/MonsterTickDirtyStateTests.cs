using Microsoft.Extensions.Logging.Abstractions;
using Shared.Infrastructure.Messages;
using Shared.Infrastructure.Spawn;
using Shared.Packet.Packets;

namespace Server.Tests.Monster;

/// <summary>
/// AC 증분7(§5.2): 몬스터 상태 브로드캐스트 dirty-flag. 위치·회전·HP·페이즈가 직전 송신과 같으면 S_MonsterState 를 생략한다.
/// → Idle 경비 몬스터는 트래픽 0(대량 몬스터 스케일). Chase/Patrol 은 매 틱 변하므로 그대로 송신.
/// </summary>
public class MonsterTickDirtyStateTests
{
    private static global::Server.Room.Room NewRoom()
        => new(1,
            new List<PlayerInfo> { new() { UserId = 100, Nickname = "A", SpawnIndex = 0 } },
            NullLogger<global::Server.Room.Room>.Instance);

    private static void SpawnCreepyDemon(global::Server.Room.Room room)
        => room.SpawnMonsters(
            new List<MonsterSpawnDef> { new("creepy_demon", 0f, 0f, 0f, 0f, 1, 0, Array.Empty<PatrolPoint>()) },
            new MapBounds(0f, 0f, 400f, 400f));

    [Fact]
    public void Idle_몬스터는_첫틱만_송신하고_이후_변화없으면_생략한다()
    {
        var room = NewRoom();
        room.InitPlayerState(100, "A", 0, 100f, 0f, 0f, 0f); // 멀리 = aggro 밖 → Idle(제자리)
        room.MarkJoined(100);
        SpawnCreepyDemon(room);

        // 첫 틱: 아직 송신 이력 없음 → dirty → 1건 송신.
        Assert.Single(room.TickMonsters(0.1f, 1_000_000).OfType<S_MonsterState>());

        // 이후 틱: 위치·회전·HP·페이즈 불변 → 생략(트래픽 0).
        Assert.Empty(room.TickMonsters(0.1f, 1_000_100).OfType<S_MonsterState>());
        Assert.Empty(room.TickMonsters(0.1f, 1_000_200).OfType<S_MonsterState>());
    }

    [Fact]
    public void Chase_몬스터는_매틱_이동하므로_매틱_송신한다()
    {
        var room = NewRoom();
        room.InitPlayerState(100, "A", 0, 3f, 0f, 0f, 0f); // aggro 안·attack 밖 → Chase(추격 이동)
        room.MarkJoined(100);
        SpawnCreepyDemon(room);

        // 추격 중 = 매 틱 위치가 변함 → 매 틱 송신.
        Assert.Single(room.TickMonsters(0.1f, 1_000_000).OfType<S_MonsterState>());
        Assert.Single(room.TickMonsters(0.1f, 1_000_100).OfType<S_MonsterState>());
        Assert.Single(room.TickMonsters(0.1f, 1_000_200).OfType<S_MonsterState>());
    }
}
