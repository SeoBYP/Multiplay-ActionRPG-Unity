using Microsoft.Extensions.Logging.Abstractions;
using Shared.Infrastructure.Abilities;
using Shared.Infrastructure.Messages;
using Shared.Infrastructure.Spawn;
using Shared.Packet.Packets;

namespace Server.Tests.Monster;

/// <summary>
/// AC-E3: 몬스터 피해가 레벨·등급으로 스케일되는지 — <b>틱 경로 실배선</b> 검증.
/// 설계 = docs/wiki/monster-leveling.md §4.2.
///
/// 산식(<c>StatCombatMath.MeleeDamage</c>)은 건드리지 않았다. 틀린 건 base 였고 여기서 그것만 스케일한다.
/// </summary>
public class MonsterLevelDamageTests
{
    private static global::Server.Room.Room NewRoom()
        => new(1,
            new List<PlayerInfo> { new() { UserId = 100, Nickname = "A", SpawnIndex = 0 } },
            NullLogger<global::Server.Room.Room>.Instance);

    private static readonly MapBounds Bounds = new(0f, 0f, 400f, 400f);

    /// <summary>사거리 안(공격 페이즈)에 플레이어를 두고 1틱 돌려 실제 피해를 뽑는다.</summary>
    private static int DamageOnTick(int mapLevel, int playerDefense)
    {
        var room = NewRoom();
        // 몬스터는 원점, 플레이어를 바로 옆에 → attack 사거리 진입.
        room.InitPlayerState(100, "A", 0, 0.5f, 0f, 0f, 0f, maxHealth: 100_000, defense: playerDefense);
        room.MarkJoined(100);
        room.SpawnMonsters(
            new List<MonsterSpawnDef> { new("creepy_demon", 0f, 0f, 0f, 0f, 1, 0, Array.Empty<PatrolPoint>()) },
            Bounds,
            mapLevel);

        var effects = room.TickMonsters(0.1f, 1_000_000).OfType<S_ApplyEffect>().ToList();
        var dmg = effects.Single(e => e.Amount != 0);
        return -dmg.Amount; // Amount 는 Health 델타(음수)
    }

    [Fact]
    public void L1은_저작값_그대로다_동작보존()
    {
        // 레벨 도입이 기존 밸런스를 바꾸면 안 된다.
        int baseDmg = AbilityCatalog.Get("creepy_demon_attack")!.BaseDamage;
        int expected = Math.Max(1, baseDmg - 5);

        Assert.Equal(expected, DamageOnTick(mapLevel: 0, playerDefense: 5));
    }

    [Fact]
    public void 레벨이_오르면_피해가_커진다_밸런스_수정의_핵심()
    {
        // C1c 에서 본 버그: base 고정 + DEF 성장 → 고레벨에서 1 데미지.
        // 이제 레벨을 주면 base 가 함께 커져 순피해가 유지된다.
        int atL1 = DamageOnTick(mapLevel: 1, playerDefense: 5);
        int atL6 = DamageOnTick(mapLevel: 6, playerDefense: 15); // L6 플레이어 DEF

        Assert.True(atL6 > atL1,
            $"L6 몬스터(피해 {atL6})가 L6 플레이어에게 L1({atL1})보다 강해야 한다 — 이게 안 되면 밸런스가 안 고쳐진 것");
    }

    [Fact]
    public void 고레벨에서도_바닥에_눌리지_않는다()
    {
        // 원래 증상: L19 플레이어(DEF 41) 앞에서 전 몬스터가 1 데미지.
        int dmg = DamageOnTick(mapLevel: 19, playerDefense: 41);

        Assert.True(dmg > 1, $"L19 대역에서 피해가 {dmg} — 바닥에 눌렸다(레벨링 이전의 그 버그)");
    }

}
