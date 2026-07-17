using Microsoft.Extensions.Logging.Abstractions;
using Shared.Infrastructure.Messages;
using Shared.Infrastructure.Abilities;
using Shared.Infrastructure.Monsters;
using Shared.Infrastructure.Spawn;

namespace Server.Tests.Monster;

/// <summary>
/// AC-E2: 몬스터 레벨·등급 저작과 스폰 시 확정. 설계 = docs/wiki/monster-leveling.md §4.1.
///
/// 이 증분은 **동작 보존**이어야 한다 — 레벨을 저작하지 않은 기존 데이터는 전부 L1 로 떨어지고,
/// L1 스케일은 항등이라 스탯이 하나도 바뀌지 않는다(codemap §2.62: 증분 경계 교훈).
/// </summary>
public class MonsterSpawnLevelTests
{
    private static global::Server.Room.Room NewRoom()
        => new(1,
            new List<PlayerInfo> { new() { UserId = 100, Nickname = "A", SpawnIndex = 0 } },
            NullLogger<global::Server.Room.Room>.Instance);

    private static MonsterSpawnDef Def(string id = "creepy_demon", int level = 0, MonsterTier tier = MonsterTier.Normal)
        => new(id, 0f, 0f, 0f, 0f, 1, 0, Array.Empty<PatrolPoint>(), 0, 0, level, tier);

    private static readonly MapBounds Bounds = new(0f, 0f, 400f, 400f);

    // ── 레벨 해석 규칙(단일 구현) ──

    [Fact]
    public void 스폰_레벨이_맵_기본을_이긴다()
    {
        // 같은 던전 안에서 엘리트/보스만 대역을 올릴 수 있어야 한다.
        Assert.Equal(9, MapSpawnLayout.ResolveLevel(spawnLevel: 9, mapLevel: 3));
    }

    [Fact]
    public void 스폰_레벨이_없으면_맵_기본을_쓴다()
    {
        // 던전 한 줄로 전체 난이도를 조절하는 게 기본 저작 경로다.
        Assert.Equal(3, MapSpawnLayout.ResolveLevel(spawnLevel: 0, mapLevel: 3));
    }

    [Fact]
    public void 둘_다_없으면_L1이다_기존데이터_동작보존()
    {
        // 레벨을 저작하지 않은 기존 spawn-layouts.json 이 그대로 동작해야 한다.
        Assert.Equal(1, MapSpawnLayout.ResolveLevel(spawnLevel: 0, mapLevel: 0));
    }

    // ── 스폰 시 확정 ──

    [Fact]
    public void 레벨_미저작이면_스탯이_저작값_그대로다_동작보존()
    {
        var room = NewRoom();
        room.SpawnMonsters(new List<MonsterSpawnDef> { Def() }, Bounds); // mapMonsterLevel 생략 = 0

        var m = room.GetAllMonsters().Single();

        Assert.Equal(1, m.Level);
        Assert.Equal(MonsterTier.Normal, m.Tier);
        Assert.Equal(MonsterCatalog.Get("creepy_demon").MaxHp, m.MaxHp); // 스케일 이전 값과 동일
        Assert.Equal(m.MaxHp, m.Hp);
    }

    [Fact]
    public void 맵_기본_레벨이_스폰에_반영된다()
    {
        var room = NewRoom();
        room.SpawnMonsters(new List<MonsterSpawnDef> { Def() }, Bounds, mapMonsterLevel: 5);

        var m = room.GetAllMonsters().Single();

        Assert.Equal(5, m.Level);
        Assert.Equal(MonsterLevelScaling.Hp(MonsterCatalog.Get("creepy_demon").MaxHp, 5), m.MaxHp);
        Assert.True(m.MaxHp > MonsterCatalog.Get("creepy_demon").MaxHp, "레벨이 오르면 HP 가 커져야 한다");
    }

    [Fact]
    public void 스폰별_override_가_맵_기본을_이긴다()
    {
        var room = NewRoom();
        room.SpawnMonsters(
            new List<MonsterSpawnDef> { Def(level: 10) },
            Bounds,
            mapMonsterLevel: 3);

        Assert.Equal(10, room.GetAllMonsters().Single().Level);
    }

    [Fact]
    public void 등급이_스폰에_확정되고_HP에_반영된다()
    {
        var room = NewRoom();
        room.SpawnMonsters(new List<MonsterSpawnDef> { Def(tier: MonsterTier.Elite) }, Bounds);

        var m = room.GetAllMonsters().Single();

        Assert.Equal(MonsterTier.Elite, m.Tier);
        Assert.Equal(MonsterLevelScaling.Hp(MonsterCatalog.Get("creepy_demon").MaxHp, 1, MonsterTier.Elite), m.MaxHp);
    }

    [Fact]
    public void 같은_맵에서_잡몹과_보스가_공존한다()
    {
        // 등급이 스폰별이라 같은 monsterId 를 잡몹으로도 보스로도 쓸 수 있다(설계 §3).
        var room = NewRoom();
        room.SpawnMonsters(
            new List<MonsterSpawnDef> { Def(), Def(tier: MonsterTier.Boss) },
            Bounds,
            mapMonsterLevel: 4);

        var all = room.GetAllMonsters().OrderBy(x => x.MaxHp).ToList();

        Assert.Equal(2, all.Count);
        Assert.Equal(MonsterTier.Normal, all[0].Tier);
        Assert.Equal(MonsterTier.Boss, all[1].Tier);
        Assert.Equal(4, all[0].Level);
        Assert.Equal(4, all[1].Level); // 레벨은 맵 기본 공유 — 등급만 다르다(직교)
        Assert.True(all[1].MaxHp > all[0].MaxHp * 4, "보스는 HP ×6 이라 확연히 두껍다");
    }

    [Fact]
    public void 던전_대역이_단조_증가한다_AC_F()
    {
        // 저작된 진행 곡선. 대역이 뒤섞이면 플레이어가 어느 던전을 가야 할지 알 수 없다.
        var band = new[]
        {
            ("dungeon_01", 1), ("dungeon_02", 6), ("dungeon_03", 12), ("dungeon_04", 20), ("dungeon_05", 30),
        };

        int prevLevel = 0;
        long prevExp = 0;
        foreach (var (mapId, expected) in band)
        {
            var layout = SpawnLayoutTable.Get(mapId);
            Assert.Equal(expected, layout.MonsterLevel);
            Assert.True(layout.MonsterLevel > prevLevel, $"{mapId}: 레벨이 단조 증가해야 한다");
            Assert.True(layout.ExpReward > prevExp, $"{mapId}: 보상도 대역과 함께 커져야 한다");
            prevLevel = layout.MonsterLevel;
            prevExp = layout.ExpReward;
        }
    }

    [Fact]
    public void 상위_던전일수록_등급_구성이_강해진다_AC_F()
    {
        // 보스는 상위 대역에만. dungeon_01(입문)에 보스가 있으면 진행 곡선이 무너진다.
        Assert.DoesNotContain(SpawnLayoutTable.Get("dungeon_01").Monsters, m => m.Tier != MonsterTier.Normal);

        Assert.Contains(SpawnLayoutTable.Get("dungeon_02").Monsters, m => m.Tier == MonsterTier.Boss);
        Assert.Contains(SpawnLayoutTable.Get("dungeon_03").Monsters, m => m.Tier == MonsterTier.Elite);
        Assert.Contains(SpawnLayoutTable.Get("dungeon_04").Monsters, m => m.Tier == MonsterTier.Boss);

        // 최상급 = 잡몹이 없다(전원 엘리트 이상).
        Assert.DoesNotContain(SpawnLayoutTable.Get("dungeon_05").Monsters, m => m.Tier == MonsterTier.Normal);
    }

    [Fact]
    public void 보스_몬스터는_보스_등급으로_배치된다()
    {
        // leviathan 은 base 40/slam 90 인 보스인데 dungeon_02 에서 Normal 로 스폰되고 있었다(AC-F 에서 교정).
        foreach (var mapId in new[] { "dungeon_02", "dungeon_04", "dungeon_05" })
        {
            var levis = SpawnLayoutTable.Get(mapId).Monsters.Where(m => m.MonsterId == "leviathan").ToList();
            Assert.NotEmpty(levis);
            Assert.All(levis, m => Assert.Equal(MonsterTier.Boss, m.Tier));
        }
    }

    [Fact]
    public void E2E_던전은_L1로_고정된다()
    {
        // 테스트 픽스처 — 대역이 바뀌면 E2E 기대값(몬스터 HP·피해)이 조용히 흔들린다.
        Assert.Equal(1, SpawnLayoutTable.Get("dungeon_e2e").MonsterLevel);
    }

    [Fact]
    public void 스폰별_레벨_override_는_쓰지_않는다()
    {
        // 대역은 맵 한 줄로 조절하고, 강도 차이는 **등급**으로 낸다(레벨 override 는 필요해질 때만).
        // 섞어 쓰면 "이 몬스터가 왜 센지"를 두 곳에서 찾아야 한다.
        foreach (var mapId in new[] { "dungeon_01", "dungeon_02", "dungeon_03", "dungeon_04", "dungeon_05", "dungeon_e2e" })
            Assert.All(SpawnLayoutTable.Get(mapId).Monsters, m => Assert.Equal(0, m.Level));
    }

    [Fact]
    public void 저작된_대역이_실제_스탯으로_이어진다_E5()
    {
        // 밸런스가 진짜로 바뀌었는지 — dungeon_02(L6) 몬스터는 dungeon_01(L1)보다 두껍고 아파야 한다.
        var d1 = SpawnLayoutTable.Get("dungeon_01");
        var d2 = SpawnLayoutTable.Get("dungeon_02");

        int baseHp = MonsterCatalog.Get("creepy_demon").MaxHp;
        int hp1 = MonsterLevelScaling.Hp(baseHp, MapSpawnLayout.ResolveLevel(0, d1.MonsterLevel));
        int hp6 = MonsterLevelScaling.Hp(baseHp, MapSpawnLayout.ResolveLevel(0, d2.MonsterLevel));

        Assert.Equal(baseHp, hp1);                 // L1 = 항등
        Assert.True(hp6 > hp1 * 2, $"L6 HP({hp6})가 L1({hp1})의 2배 이상이어야 한다");

        // C1c 에서 본 증상: L6 플레이어(DEF 15) 앞에서 creepy_demon 피해가 1.
        int baseDmg = AbilityCatalog.Get("creepy_demon_attack")!.BaseDamage;
        int dmg6 = MonsterLevelScaling.Damage(baseDmg, 6);
        Assert.True(dmg6 - 15 > 5, $"L6 순피해가 {dmg6 - 15} — 바닥(1)에서 벗어나야 밸런스가 고쳐진 것");
    }
}
