using Microsoft.Extensions.Logging.Abstractions;
using Script.System.GamePlayAbilitySystem;
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
        room.AddPlayer(100, "A", 0, 100f, 0f, 0f, 0f); // 멀리 = aggro 밖 → Idle(제자리)
        room.MarkJoined(100);
        SpawnCreepyDemon(room);

        // 첫 틱: 아직 송신 이력 없음 → dirty → 1건 송신.
        Assert.Single(room.Tick(0.1f, 1_000_000).OfType<S_MonsterState>());

        // 이후 틱: 위치·회전·HP·페이즈 불변 → 생략(트래픽 0).
        Assert.Empty(room.Tick(0.1f, 1_000_100).OfType<S_MonsterState>());
        Assert.Empty(room.Tick(0.1f, 1_000_200).OfType<S_MonsterState>());
    }

    [Fact]
    public void HP가_바뀌면_이동이_없어도_다음틱이_재전송한다_자가교정()
    {
        // AC-C3-hotfix (D2 회귀 봉합) — **불변식: HP 변화는 항상 다음 틱이 재전송한다.**
        //
        // 왜 필요한가: 데미지 경로(CombatHandler)는 S_MonsterState 를 즉시 전송한다. 그런데 틱은
        // 패킷을 **만든 뒤 나중에 전송**하므로, 그 사이에 데미지가 들어가면 **옛 HP 패킷이 새 HP 뒤에 도착**한다.
        // 이때 틱이 "이미 보냈다"고 마킹해 두면 다음 틱이 정정하지 않아 클라 HP 가 **영구 고착**된다.
        // → 데미지 경로는 마킹하지 않고, 틱이 무조건 재전송해 자가 교정하도록 고정한다.
        var room = NewRoom();
        room.AddPlayer(100, "A", 0, 100f, 0f, 0f, 0f); // 멀리 = Idle(이동/회전/페이즈 불변)
        room.MarkJoined(100);
        SpawnCreepyDemon(room);

        const long t0 = 1_000_000;
        Assert.Single(room.Tick(0.1f, t0).OfType<S_MonsterState>());        // 첫 틱 = 송신
        Assert.Empty(room.Tick(0.1f, t0 + 100).OfType<S_MonsterState>());   // 무변화 = 생략(증분7 목적 유지)

        // 플레이어 공격 경로가 하는 일 = 서버 권위 HP 차감(+ 즉시 전송).
        int id = room.Actors.Monsters()[0].InstanceId;
        var dmg = new[] { GameplayAttributeModifier.Create(EGameplayAttribute.Health, -10, EModifierType.Additive) };
        var (hit, newHp, _) = room.Actors.DamageMonster(id, dmg);
        Assert.True(hit);

        // 이동이 전혀 없어도 HP 가 바뀌었으니 다음 틱이 **반드시** 재전송해야 한다(스테일 고착 방지).
        var state = room.Tick(0.1f, t0 + 200).OfType<S_MonsterState>().Single();
        Assert.Equal(newHp, state.Hp);
        Assert.Equal(id, state.InstanceId);
    }

    [Fact]
    public void 데미지_경로가_송신마킹하면_자가교정이_깨진다_회귀가드()
    {
        // D2 회귀의 **정확한 재현**: 데미지 경로가 즉시 전송 후 MarkStateSent 까지 하면(구 CombatHandler 동작),
        // 틱은 "이미 보냈다"고 보고 정정을 포기한다 → 스테일이 뒤늦게 도착했을 때 클라 HP 가 영구 고착.
        //
        // 이 테스트는 **그 인과를 문서화·고정**한다: 마킹하면 정정 패킷이 사라진다는 사실을 못 박아,
        // 누군가 CombatHandler 에 MarkStateSent 를 다시 넣으면 "왜 안 되는지"가 여기 남아 있게 한다.
        // (현 프로덕션 경로는 마킹하지 않으므로 위 테스트가 자가 교정을 보장한다.)
        var room = NewRoom();
        room.AddPlayer(100, "A", 0, 100f, 0f, 0f, 0f); // Idle
        room.MarkJoined(100);
        SpawnCreepyDemon(room);

        const long t0 = 1_000_000;
        room.Tick(0.1f, t0); // 첫 송신 + 마킹

        var monster = room.Actors.Monsters()[0];
        var dmg = new[] { GameplayAttributeModifier.Create(EGameplayAttribute.Health, -10, EModifierType.Additive) };
        room.Actors.DamageMonster(monster.InstanceId, dmg);
        monster.MarkStateSent(); // ← 구 CombatHandler 가 하던 짓(회귀 재현)

        // 마킹했으므로 틱은 무변화로 보고 **정정하지 않는다** = 클라가 스테일을 받았다면 복구 불가.
        Assert.Empty(room.Tick(0.1f, t0 + 200).OfType<S_MonsterState>());
    }

    [Fact]
    public void Chase_몬스터는_매틱_이동하므로_매틱_송신한다()
    {
        var room = NewRoom();
        room.AddPlayer(100, "A", 0, 3f, 0f, 0f, 0f); // aggro 안·attack 밖 → Chase(추격 이동)
        room.MarkJoined(100);
        SpawnCreepyDemon(room);

        // 추격 중 = 매 틱 위치가 변함 → 매 틱 송신.
        Assert.Single(room.Tick(0.1f, 1_000_000).OfType<S_MonsterState>());
        Assert.Single(room.Tick(0.1f, 1_000_100).OfType<S_MonsterState>());
        Assert.Single(room.Tick(0.1f, 1_000_200).OfType<S_MonsterState>());
    }
}
