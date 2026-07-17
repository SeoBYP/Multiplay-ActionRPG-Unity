using Microsoft.Extensions.Logging.Abstractions;
using Script.System.GamePlayAbilitySystem;
using Shared.Infrastructure.Messages;
using Shared.Infrastructure.Spawn;
using Shared.Packet.Packets;

namespace Server.Tests.Monster;

/// <summary>
/// AC-C3: S_MonsterState.Seq — 몬스터별 단조 증가 상태 버전. 클라가 순서 역전(스테일)을 버리는 근거.
///
/// 핵심 계약: **Seq 는 스냅샷(생성) 시점에 발급된다.** 송신 시점이 아니다 —
/// 막으려는 것이 "틱이 먼저 만든 패킷을 나중에 보내는" 생성≠송신 순서이기 때문.
/// </summary>
public class MonsterStateSeqTests
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
    public void 첫_발급은_1이라_클라_baseline_0을_항상_통과한다()
    {
        var room = NewRoom();
        room.InitPlayerState(100, "A", 0, 3f, 0f, 0f, 0f); // Chase = 매 틱 dirty
        room.MarkJoined(100);
        SpawnCreepyDemon(room);

        var first = room.TickMonsters(0.1f, 1_000_000).OfType<S_MonsterState>().Single();

        // 클라 스냅샷 baseline 은 0. 첫 상태가 0 이면 `seq <= existing.Seq` 에 걸려 **영영 반영되지 않는다**.
        Assert.Equal(1, first.Seq);
    }

    [Fact]
    public void 틱이_반복되면_Seq가_단조_증가한다()
    {
        var room = NewRoom();
        room.InitPlayerState(100, "A", 0, 3f, 0f, 0f, 0f); // Chase = 매 틱 송신
        room.MarkJoined(100);
        SpawnCreepyDemon(room);

        int s1 = room.TickMonsters(0.1f, 1_000_000).OfType<S_MonsterState>().Single().Seq;
        int s2 = room.TickMonsters(0.1f, 1_000_100).OfType<S_MonsterState>().Single().Seq;
        int s3 = room.TickMonsters(0.1f, 1_000_200).OfType<S_MonsterState>().Single().Seq;

        Assert.True(s1 < s2 && s2 < s3, $"Seq 가 단조 증가해야 한다: {s1}, {s2}, {s3}");
    }

    [Fact]
    public void 몬스터마다_Seq가_독립이다()
    {
        var room = NewRoom();
        room.InitPlayerState(100, "A", 0, 3f, 0f, 0f, 0f);
        room.MarkJoined(100);
        room.SpawnMonsters(
            new List<MonsterSpawnDef>
            {
                new("creepy_demon", 0f, 0f, 0f, 0f, 1, 0, Array.Empty<PatrolPoint>()),
                new("creepy_demon", 1f, 0f, 1f, 0f, 1, 0, Array.Empty<PatrolPoint>()),
            },
            new MapBounds(0f, 0f, 400f, 400f));

        var states = room.TickMonsters(0.1f, 1_000_000).OfType<S_MonsterState>().ToList();

        // 방 전역 카운터면 두 몬스터가 1,2 를 나눠 갖는다 → 몬스터별 baseline 비교가 깨진다(둘 중 하나가 2 로 시작).
        Assert.Equal(2, states.Count);
        Assert.All(states, s => Assert.Equal(1, s.Seq));
    }

    [Fact]
    public void 데미지_스냅샷이_틱_스냅샷보다_나중이면_Seq가_더_크다_순서역전_무효화()
    {
        // D2 의 정확한 상황을 서버 쪽에서 재현한다:
        //   틱이 옛 HP 로 패킷을 **만들고**(아직 송신 전) → 데미지가 HP 를 깎고 새 패킷을 만든다.
        // 이때 Seq(틱) < Seq(데미지) 여야 클라가 "나중에 도착한 옛 HP"를 버릴 수 있다.
        var room = NewRoom();
        room.InitPlayerState(100, "A", 0, 3f, 0f, 0f, 0f); // Chase = 틱이 매번 송신
        room.MarkJoined(100);
        SpawnCreepyDemon(room);

        // ① 틱이 패킷 생성(= 이 시점 HP 스냅샷). RoomTickService 는 아직 안 보냈다고 가정.
        var tickPacket = room.TickMonsters(0.1f, 1_000_000).OfType<S_MonsterState>().Single();

        // ② 그 사이 데미지 — CombatHandler 가 하는 일(HP 차감 + 새 스냅샷 발급).
        var monster = room.GetAllMonsters()[0];
        var dmg = new[] { GameplayAttributeModifier.Create(EGameplayAttribute.Health, -10, EModifierType.Additive) };
        var (hit, newHp, _) = room.DamageMonster(monster.InstanceId, dmg);
        Assert.True(hit);
        int damageSeq = monster.NextSeq();

        // ③ 계약: 나중에 만들어진 상태가 더 큰 Seq. 도착 순서가 뒤집혀도 클라가 ①을 버린다.
        Assert.True(damageSeq > tickPacket.Seq,
            $"데미지 스냅샷 Seq({damageSeq})가 틱 스냅샷 Seq({tickPacket.Seq})보다 커야 순서 역전을 무효화할 수 있다");
        Assert.True(newHp < tickPacket.Hp, "데미지 후 HP 가 틱 스냅샷보다 낮아야 이 시나리오가 성립한다");
    }
}
