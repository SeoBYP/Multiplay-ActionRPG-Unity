using Microsoft.Extensions.Logging.Abstractions;
using Script.System.GamePlayAbilitySystem;
using Server.PacketHandler.Handler;
using Server.Room;
using Server.Tests.Fakes;
using Shared.Infrastructure.Messages;
using Shared.Infrastructure.Spawn;
using Shared.Packet.Packets;

namespace Server.Tests.Combat;

/// <summary>
/// S1 — C_Attack 의 SkillId 신뢰 경계.
///
/// 지금 서버는 <c>AbilityCatalog.Get(skillId)</c> 가 null 이 아니면 발동시킨다. 카탈로그에는
/// 몬스터 어빌리티(100~109)와 테스트 어빌리티(105 = 데미지 9999·쿨다운 50ms)도 같이 들어 있어서,
/// 클라가 SkillId 만 바꿔 보내면 그 어빌리티가 <b>플레이어 이름으로</b> 나간다.
///
/// 불변식: 플레이어가 발동할 수 있는 것은 플레이어 어빌리티뿐이다.
/// </summary>
public class AttackAbilityTrustTests
{
    private const long UserId = 100;

    /// <summary>플레이어를 몬스터 정면 1m 에 세운 방. 대조군(basic_swing)이 맞는지로 배치를 검증한다.</summary>
    private static (Session session, global::Server.Room.Room room) PlayerFacingMonster()
    {
        var manager = new RoomManager(
            NullLogger<RoomManager>.Instance,
            NullLogger<global::Server.Room.Room>.Instance,
            new FakeRoomLifecyclePublisher(),
            new FakeDungeonResultPublisher(),
            new FakeLootPickupPublisher());

        var room = new global::Server.Room.Room(
            1,
            new List<PlayerInfo> { new() { UserId = UserId, Nickname = "A", SpawnIndex = 0 } },
            NullLogger<global::Server.Room.Room>.Instance);

        room.AddPlayer(UserId, "A", 0, 0f, 0f, -1f, 0f);
        room.MarkJoined(UserId);
        room.SpawnMonsters(
            new List<MonsterSpawnDef> { new("creepy_demon", 0f, 0f, 0f, 0f, 1, 0, Array.Empty<PatrolPoint>()) },
            new MapBounds(0f, 0f, 40f, 40f));

        var session = TestSessionFactory.Create(manager, sessionId: 1, userId: UserId);
        session.Room = room;
        return (session, room);
    }

    private static int MonsterHp(global::Server.Room.Room room)
        => room.Actors.Monsters().Single().Gas[EGameplayAttribute.Health];

    [Fact]
    public async Task 대조군_플레이어_기본공격은_정면의_몬스터에게_피해를_준다()
    {
        var (session, room) = PlayerFacingMonster();
        int before = MonsterHp(room);

        await CombatHandler.HandleAttack(session, new C_Attack { SkillId = 0 }, CancellationToken.None);

        Assert.True(MonsterHp(room) < before, "배치가 틀렸다 — 이 대조군이 실패하면 아래 테스트는 의미가 없다.");
    }

    [Theory]
    [InlineData(105)] // test_brute_attack — 데미지 9999, 쿨다운 50ms
    [InlineData(109)] // leviathan_slam   — 보스 전용, 데미지 90
    [InlineData(100)] // creepy_demon_attack — 몬스터 평타
    public async Task 플레이어는_몬스터_어빌리티를_발동할_수_없다(int monsterSkillId)
    {
        var (session, room) = PlayerFacingMonster();
        int before = MonsterHp(room);

        await CombatHandler.HandleAttack(session, new C_Attack { SkillId = monsterSkillId }, CancellationToken.None);

        Assert.Equal(before, MonsterHp(room));
    }
}
