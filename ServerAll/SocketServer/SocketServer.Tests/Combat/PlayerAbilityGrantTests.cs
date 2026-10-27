using Microsoft.Extensions.Logging.Abstractions;
using Script.System.GamePlayAbilitySystem;
using Server.Actors;
using Server.PacketHandler.Handler;
using Server.Room;
using Server.Tests.Fakes;
using Shared.Infrastructure.Abilities;
using Shared.Infrastructure.Messages;
using Shared.Infrastructure.Spawn;
using Shared.Packet.Packets;

namespace Server.Tests.Combat;

/// <summary>
/// 플레이어 보유 어빌리티 = 기본기(<see cref="PlayerActor.BasicAbilityIds"/>) + 게임 시작 메시지의 장착 목록(<c>PlayerInfo.AbilityIds</c>).
/// </summary>
public class PlayerAbilityGrantTests
{
    private const long UserId = 100;

    public static IEnumerable<object[]> BasicAbilityIds()
        => PlayerActor.BasicAbilityIds.Select(id => new object[] { id });

    private static RoomManager NewManager() => new(
        NullLogger<RoomManager>.Instance,
        NullLogger<global::Server.Room.Room>.Instance,
        new FakeRoomLifecyclePublisher(),
        new FakeDungeonResultPublisher(),
        new FakeLootPickupPublisher());

    private static global::Server.Room.Room NewRoom()
        => new(1, new List<PlayerInfo> { new() { UserId = UserId, Nickname = "A" } },
            NullLogger<global::Server.Room.Room>.Instance);

    private static AbilitySystemComponent GasOf(global::Server.Room.Room room)
        => room.Actors.GetMember(UserId)!.Actor.Gas;

    [Theory]
    [MemberData(nameof(BasicAbilityIds))]
    public void 기본기_id는_모두_어빌리티_카탈로그에_있다(string abilityId)
    {
        Assert.NotNull(AbilityCatalog.Get(abilityId));
    }

    [Theory]
    [MemberData(nameof(BasicAbilityIds))]
    public void 입장한_플레이어는_기본기를_모두_보유한다(string abilityId)
    {
        var room = NewRoom();

        room.AddPlayer(UserId, "A", 0, 0f, 0f, 0f, 0f);

        Assert.True(GasOf(room).HasAbility(abilityId));
    }

    [Fact]
    public void 장착_목록으로_받은_어빌리티를_보유하고_받지_않은_것은_보유하지_않는다()
    {
        var room = NewRoom();

        room.AddPlayer(UserId, "A", 0, 0f, 0f, 0f, 0f, abilityIds: new[] { "leviathan_slam" });

        Assert.True(GasOf(room).HasAbility("leviathan_slam"));
        Assert.False(GasOf(room).HasAbility("test_brute_attack"));
    }

    [Fact]
    public void 게임_시작_메시지의_장착_목록이_액터에_부여된다()
    {
        var manager = NewManager();
        var players = new List<PlayerInfo>
        {
            new() { UserId = UserId, Nickname = "A", SpawnIndex = 0, AbilityIds = new[] { "leviathan_slam" } },
        };
        var message = new GameStartRequestedMessage { RoomId = 1, PlayerInfos = players };

        var room = manager.CreateRoom(1, players, message);

        Assert.NotNull(room);
        Assert.True(GasOf(room!).HasAbility("leviathan_slam"));
        Assert.True(GasOf(room!).HasAbility("basic_swing"));
    }

    [Fact]
    public async Task 장착_목록으로_받은_어빌리티는_발동된다()
    {
        // test_brute_attack(105) 는 S1 에서 "보유하지 않으면 거부"되는 대상 — 부여하면 같은 패킷이 통과해야 한다.
        var room = NewRoom();
        room.AddPlayer(UserId, "A", 0, 0f, 0f, -1f, 0f, abilityIds: new[] { "test_brute_attack" });
        room.MarkJoined(UserId);
        room.SpawnMonsters(
            new List<MonsterSpawnDef> { new("creepy_demon", 0f, 0f, 0f, 0f, 1, 0, Array.Empty<PatrolPoint>()) },
            new MapBounds(0f, 0f, 40f, 40f));
        var session = TestSessionFactory.Create(NewManager(), sessionId: 1, userId: UserId);
        session.Room = room;
        int before = room.Actors.Monsters().Single().Gas[EGameplayAttribute.Health];

        await CombatHandler.HandleAttack(session, new C_Attack { SkillId = 105 }, CancellationToken.None);

        var monsters = room.Actors.Monsters();
        Assert.True(monsters.Count == 0 || monsters.Single().Gas[EGameplayAttribute.Health] < before);
    }
}
