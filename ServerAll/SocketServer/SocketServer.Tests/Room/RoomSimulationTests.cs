using Microsoft.Extensions.Logging.Abstractions;
using Script.System.GamePlayAbilitySystem;
using Server.Actors;
using Server.Monster;
using Server.Room;
using Shared.Infrastructure.Spawn;
using Shared.Packet.Packets;

namespace Server.Tests.Room;

/// <summary>
/// 한 틱 시뮬레이션 — <b>방 없이</b> 저장소만으로 돌린다(Room 에서 떼어낸 덕).
///
/// <para>여기서 고정하는 경계: 시뮬레이션은 <b>진행 판정을 하지 않는다</b>.
/// HP 0 을 감지하면 <c>DownedUserIds</c> 로 알려줄 뿐 <c>S_PlayerDead</c>·<c>S_DungeonFailed</c> 는 만들지 않는다 —
/// 그건 방(Room)이 붙인다. 이 경계가 무너지면 시뮬레이션이 다시 방 상태를 알게 된다.</para>
/// </summary>
public class RoomSimulationTests
{
    private static readonly MapBounds Bounds = new(0f, 0f, 40f, 40f);

    private static (RoomSimulation Sim, ActorStore Store) NewSim()
    {
        var store = new ActorStore();
        int effectId = 0;
        return (new RoomSimulation(store, () => ++effectId, NullLogger.Instance), store);
    }

    private static RoomMember AddJoinedPlayer(ActorStore store, long userId, float x, float z, int maxHp = 100)
    {
        var member = store.AddPlayer(userId, "P", 0, x, 0f, z, 0f,
            attackPower: 0, defense: 0, maxHp: maxHp, maxMana: 50);
        member.HasJoined = true;
        return member;
    }

    /// <summary>사거리(1.3) 안에 서 있는 creepy_demon 을 원점에 놓는다.</summary>
    private static MonsterActor AddDemon(ActorStore store)
    {
        var monster = new MonsterActor(store.NextMonsterInstanceId())
        {
            MonsterId = "creepy_demon",
            Phase = MonsterPhase.Idle,
        };
        monster.Gas.DefineResource(EGameplayAttribute.Health, 40);
        store.Add(monster);
        return monster;
    }

    [Fact]
    public void 사거리_안_타깃에_발동신호와_데미지를_낸다()
    {
        var (sim, store) = NewSim();
        AddJoinedPlayer(store, 100, x: 0.5f, z: 0f);
        AddDemon(store);

        var (packets, downed) = sim.Tick(0.1f, 1_000_000, Bounds);

        Assert.Single(packets.OfType<S_AbilityActivated>());
        var dmg = packets.OfType<S_ApplyEffect>().Single();
        Assert.Equal(100, dmg.TargetId);
        Assert.True(dmg.Amount < 0, "데미지는 음수 Health 델타");
        Assert.Empty(downed); // 한 대로는 안 죽는다
    }

    [Fact]
    public void 미입장_참가자는_타깃이_아니다()
    {
        // 입장 전에 죽으면 S_PlayerDead 가 빈 방에 발행돼 유실된다 — 그래서 타깃 자격에서 뺀다.
        var (sim, store) = NewSim();
        store.AddPlayer(100, "P", 0, 0.5f, 0f, 0f, 0f, 0, 0, 100, 50); // HasJoined = false
        AddDemon(store);

        var (packets, _) = sim.Tick(0.1f, 1_000_000, Bounds);

        Assert.Empty(packets.OfType<S_ApplyEffect>());
    }

    [Fact]
    public void 끊긴_참가자는_타깃이_아니다()
    {
        var (sim, store) = NewSim();
        var member = AddJoinedPlayer(store, 100, 0.5f, 0f);
        member.DisconnectedAtMs = 1L; // 재접속 유예 중
        AddDemon(store);

        var (packets, _) = sim.Tick(0.1f, 1_000_000, Bounds);

        Assert.Empty(packets.OfType<S_ApplyEffect>());
    }

    [Fact]
    public void 다운_태그가_붙은_참가자는_타깃이_아니다()
    {
        var (sim, store) = NewSim();
        var member = AddJoinedPlayer(store, 100, 0.5f, 0f);
        member.Actor.Gas.AddTag(GameplayTags.Dead);
        AddDemon(store);

        var (packets, _) = sim.Tick(0.1f, 1_000_000, Bounds);

        Assert.Empty(packets.OfType<S_ApplyEffect>());
    }

    [Fact]
    public void 무적_창_안이면_발동신호는_나가되_데미지는_없다()
    {
        // 헛스윙: 쿨다운은 소모되지만 피해가 없다. 스윙 애니는 나가야 하므로 발동 신호는 유지.
        var (sim, store) = NewSim();
        var member = AddJoinedPlayer(store, 100, 0.5f, 0f);
        member.Actor.InvulnerableUntilMs = 1_000_500;
        AddDemon(store);

        var (packets, downed) = sim.Tick(0.1f, 1_000_000, Bounds);

        Assert.Single(packets.OfType<S_AbilityActivated>());
        Assert.Empty(packets.OfType<S_ApplyEffect>());
        Assert.Empty(downed);
    }

    [Fact]
    public void HP0이_되면_DownedUserIds로_알리고_진행_패킷은_만들지_않는다()
    {
        // 이 경계가 이번 분리의 핵심 — S_PlayerDead / S_DungeonFailed 는 Room 이 붙인다.
        var (sim, store) = NewSim();
        AddJoinedPlayer(store, 100, x: 0.5f, z: 0f, maxHp: 1); // 한 대에 죽는다
        AddDemon(store);

        var (packets, downed) = sim.Tick(0.1f, 1_000_000, Bounds);

        Assert.Equal(new[] { 100L }, downed);
        Assert.Empty(packets.OfType<S_PlayerDead>());
        Assert.Empty(packets.OfType<S_DungeonFailed>());
    }

    [Fact]
    public void 쿨다운_안에서는_다시_발동하지_않는다()
    {
        var (sim, store) = NewSim();
        AddJoinedPlayer(store, 100, 0.5f, 0f);
        AddDemon(store);

        Assert.Single(sim.Tick(0.1f, 1_000_000, Bounds).Packets.OfType<S_ApplyEffect>());
        Assert.Empty(sim.Tick(0.1f, 1_000_100, Bounds).Packets.OfType<S_ApplyEffect>()); // 100ms 뒤
        Assert.Single(sim.Tick(0.1f, 1_002_000, Bounds).Packets.OfType<S_ApplyEffect>()); // 쿨다운 경과
    }

    [Fact]
    public void 변화가_없으면_상태_패킷을_생략한다_dirty_flag()
    {
        // 타깃이 없는 Idle 몬스터는 첫 틱만 상태를 보내고 이후엔 침묵한다(트래픽 0).
        var (sim, store) = NewSim();
        AddDemon(store); // 플레이어 없음 → Idle

        Assert.Single(sim.Tick(0.1f, 1_000_000, Bounds).Packets.OfType<S_MonsterState>());
        Assert.Empty(sim.Tick(0.1f, 1_000_100, Bounds).Packets.OfType<S_MonsterState>());
    }

    [Fact]
    public void 죽은_몬스터는_틱을_돌지_않는다()
    {
        var (sim, store) = NewSim();
        AddJoinedPlayer(store, 100, 0.5f, 0f);
        var monster = AddDemon(store);
        monster.Gas[EGameplayAttribute.Health] = 0;

        var (packets, _) = sim.Tick(0.1f, 1_000_000, Bounds);

        Assert.Empty(packets.OfType<S_AbilityActivated>());
        Assert.Empty(packets.OfType<S_ApplyEffect>());
    }

    [Fact]
    public void 플레이어는_마나가_자연회복된다()
    {
        var (sim, store) = NewSim();
        var member = AddJoinedPlayer(store, 100, 10f, 10f); // 몬스터 없음
        member.Actor.Gas[EGameplayAttribute.Mana] = 0;

        sim.Tick(1.0f, 1_000_000, Bounds);

        Assert.True(member.Actor.Gas[EGameplayAttribute.Mana] > 0, "틱이 마나를 회복시켜야 한다");
    }
}
