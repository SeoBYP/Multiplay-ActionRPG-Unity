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
        room.MarkJoined(100);                                // 입장 완료 = 라이브 타깃
        room.SpawnMonsters(
            new List<MonsterSpawnDef> { new("creepy_demon", 0f, 0f, 0f, 0f, 1, 0, Array.Empty<PatrolPoint>()) },
            new MapBounds(0f, 0f, 40f, 40f));

        const long t0 = 1_000_000; // LastAttackAt=0 이므로 첫 틱은 즉시 공격

        var p1 = room.TickMonsters(0.1f, t0);
        // creepy_demon 은 데미지(monster_attack_dmg) 단일 효과(CC 없음). CC 부여 검증은 arachnya 테스트가 담당.
        var atk1 = p1.OfType<S_ApplyEffect>().Single(e => e.EffectId == "monster_attack_dmg");
        Assert.Equal(100, atk1.TargetId);
        Assert.Equal(0, atk1.SourceId); // 0 = 몬스터/환경

        // 즉시 다시 틱 → 쿨다운(1500ms) 내라 공격 없음
        var p2 = room.TickMonsters(0.1f, t0 + 100);
        Assert.Empty(p2.OfType<S_ApplyEffect>());

        // 쿨다운 경과 후 → 다시 공격(데미지 패킷 1개)
        var p3 = room.TickMonsters(0.1f, t0 + 2000);
        Assert.Single(p3.OfType<S_ApplyEffect>().Where(e => e.EffectId == "monster_attack_dmg"));
    }

    [Fact]
    public void 몬스터_공격은_플레이어_Defense를_빼고_데미지를_적용한다()
    {
        var room = NewRoom();
        // creepy_demon AttackDamage=12, 플레이어 Defense=2 → 데미지 = max(1, 12-2) = 10
        room.InitPlayerState(100, "A", 0, 0.5f, 0f, 0f, 0f, attackPower: 0, defense: 2);
        room.MarkJoined(100);
        room.SpawnMonsters(
            new List<MonsterSpawnDef> { new("creepy_demon", 0f, 0f, 0f, 0f, 1, 0, Array.Empty<PatrolPoint>()) },
            new MapBounds(0f, 0f, 40f, 40f));

        var atk = room.TickMonsters(0.1f, 1_000_000).OfType<S_ApplyEffect>().Single(e => e.EffectId == "monster_attack_dmg");
        Assert.Equal(-10, atk.Amount); // 서버 권위 Health 델타(Defense 반영)

        // 서버 HP 도 같은 값으로 차감(클라 표시값 == 서버 권위).
        var hp = room.GetAllPlayerStates().Single().Hp;
        Assert.Equal(global::Server.Room.Room.DefaultMaxHp - 10, hp);
    }

    [Fact]
    public void Defense가_공격력보다_커도_최소_1_데미지는_들어간다()
    {
        var room = NewRoom();
        // creepy_demon AttackDamage=12, 플레이어 Defense=20 → max(1, 12-20) = 1 (무피해 방지)
        room.InitPlayerState(100, "A", 0, 0.5f, 0f, 0f, 0f, attackPower: 0, defense: 20);
        room.MarkJoined(100);
        room.SpawnMonsters(
            new List<MonsterSpawnDef> { new("creepy_demon", 0f, 0f, 0f, 0f, 1, 0, Array.Empty<PatrolPoint>()) },
            new MapBounds(0f, 0f, 40f, 40f));

        var atk = room.TickMonsters(0.1f, 1_000_000).OfType<S_ApplyEffect>().Single(e => e.EffectId == "monster_attack_dmg");
        Assert.Equal(-1, atk.Amount);
        Assert.Equal(global::Server.Room.Room.DefaultMaxHp - 1, room.GetAllPlayerStates().Single().Hp);
    }

    [Fact]
    public void 아라크냐_공격은_슬로우_CC를_함께_브로드캐스트한다()
    {
        // CC 부여 몬스터로 arachnya(monsters.json onHitEffectId=slow_3s) 사용 — creepy_demon 은 CC 없음.
        var room = NewRoom();
        room.InitPlayerState(100, "A", 0, 0.5f, 0f, 0f, 0f); // 사거리 안
        room.MarkJoined(100);
        room.SpawnMonsters(
            new List<MonsterSpawnDef> { new("arachnya", 0f, 0f, 0f, 0f, 1, 0, Array.Empty<PatrolPoint>()) },
            new MapBounds(0f, 0f, 40f, 40f));

        var effects = room.TickMonsters(0.1f, 1_000_000).OfType<S_ApplyEffect>().ToList();

        // 데미지 + CC(slow_3s, monsters.json) 두 효과를 함께 브로드캐스트.
        var cc = effects.Single(e => e.EffectId == "slow_3s");
        Assert.Equal(100, cc.TargetId);
        Assert.Equal(0, cc.Amount); // CC = HP 변경 없는 상태태그(GrantedTags)
    }

    [Fact]
    public void 다운된_플레이어는_몬스터_공격_대상에서_제외된다()
    {
        var room = NewRoom();
        room.InitPlayerState(100, "A", 0, 0.5f, 0f, 0f, 0f); // 사거리 안
        room.MarkJoined(100);
        room.SpawnMonsters(
            new List<MonsterSpawnDef> { new("creepy_demon", 0f, 0f, 0f, 0f, 1, 0, Array.Empty<PatrolPoint>()) },
            new MapBounds(0f, 0f, 40f, 40f));

        // 살아있을 때: 공격 발생(데미지 패킷)
        Assert.Single(room.TickMonsters(0.1f, 1_000_000).OfType<S_ApplyEffect>().Where(e => e.EffectId == "monster_attack_dmg"));

        // 다운(HP 0 보고) 처리 → 더 이상 타깃 아님 → 쿨다운 지나도 공격 없음
        room.TryMarkFailed(100);
        Assert.Empty(room.TickMonsters(0.1f, 1_000_000 + 5000).OfType<S_ApplyEffect>());
    }

    [Fact]
    public void 플레이어가_aggro밖이면_공격하지_않는다()
    {
        var room = NewRoom();
        room.InitPlayerState(100, "A", 0, 100f, 0f, 0f, 0f); // 멀리(aggro 밖)
        room.MarkJoined(100);                                // 입장은 했지만 사거리 밖
        room.SpawnMonsters(
            new List<MonsterSpawnDef> { new("creepy_demon", 0f, 0f, 0f, 0f, 1, 0, Array.Empty<PatrolPoint>()) },
            new MapBounds(0f, 0f, 400f, 400f));

        var packets = room.TickMonsters(0.1f, 1_000_000);

        Assert.Empty(packets.OfType<S_ApplyEffect>());   // 공격 없음
        Assert.NotEmpty(packets.OfType<S_MonsterState>()); // 상태 브로드캐스트는 여전히 함
    }
}
