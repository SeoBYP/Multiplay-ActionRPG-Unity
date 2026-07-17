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
/// L1 스케일은 항등이라 스탯이 하나도 바뀌지 않는다(codemap §2.66: 증분 경계 교훈).
/// </summary>
public class MonsterSpawnLevelTests
{
    private static global::Server.Room.Room NewRoom()
        => new(1,
            new List<PlayerInfo> { new() { UserId = 100, Nickname = "A", SpawnIndex = 0 } },
            NullLogger<global::Server.Room.Room>.Instance);

    private static MonsterSpawnDef Def(string id = "creepy_demon", int level = 0)
        => new(id, 0f, 0f, 0f, 0f, 1, 0, Array.Empty<PatrolPoint>(), 0, 0, level);

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
    public void E2E_던전은_L1로_고정된다()
    {
        // 테스트 픽스처 — 대역이 바뀌면 E2E 기대값(몬스터 HP·피해)이 조용히 흔들린다.
        Assert.Equal(1, SpawnLayoutTable.Get("dungeon_e2e").MonsterLevel);
    }

    [Fact]
    public void 등급은_카탈로그에서_온다_스폰이_정하지_않는다_AC_G()
    {
        // AC-G: 등급을 스폰 필드에서 **몬스터 테이블**로 옮겼다 — monsterId 가 곧 변종이다.
        // creepy_demon 은 Normal 로 저작돼 있으므로 스폰이 무엇을 하든 Normal 이다.
        var room = NewRoom();
        room.SpawnMonsters(new List<MonsterSpawnDef> { Def() }, Bounds, mapMonsterLevel: 4);

        var m = room.GetAllMonsters().Single();

        Assert.Equal(MonsterCatalog.Get("creepy_demon").Tier, m.Tier);
        Assert.Equal(4, m.Level); // 레벨은 여전히 스폰/맵이 정한다(등급과 직교)
    }

    [Fact]
    public void 등급은_스탯에_곱해지지_않는다_AC_G()
    {
        // 배율을 없앴다 — 변종은 각자 ID·스탯을 직접 저작한다.
        // 따라서 같은 base·레벨이면 등급과 무관하게 HP 가 같다(등급은 표시·연출 분류일 뿐).
        int hp = MonsterLevelScaling.Hp(MonsterCatalog.Get("creepy_demon").MaxHp, 6);
        var room = NewRoom();
        room.SpawnMonsters(new List<MonsterSpawnDef> { Def() }, Bounds, mapMonsterLevel: 6);

        Assert.Equal(hp, room.GetAllMonsters().Single().MaxHp);
    }

    [Fact]
    public void 변종은_별개_ID_로_저작된다_AC_G()
    {
        // AC-G 의 핵심: 등급 배율 대신 **변종이 자기 ID·스탯을 직접 갖는다**.
        var normal = MonsterCatalog.Get("leviathan");
        var boss = MonsterCatalog.Get("leviathan_boss");

        Assert.Equal(MonsterTier.Normal, normal.Tier);
        Assert.Equal(MonsterTier.Boss, boss.Tier);
        Assert.True(boss.MaxHp > normal.MaxHp * 4, $"보스({boss.MaxHp})는 원본({normal.MaxHp})보다 확연히 두꺼워야 한다");
        Assert.True(boss.ExpReward > normal.ExpReward, "보스 보상이 더 커야 한다");

        // 어빌리티는 원본과 같다(같은 몬스터의 변종이므로).
        Assert.Equal(normal.AbilityIds, boss.AbilityIds);
    }

    [Fact]
    public void 변종도_자기_ID_의_드롭테이블을_갖는다_AC_G()
    {
        // 등급 확률 배율을 없앴으므로, 변종에 테이블이 없으면 **아무것도 안 떨군다**.
        foreach (var id in new[] { "leviathan_boss", "undead_axemaster_elite", "wild_centaur_elite", "gargoyle_elite" })
            Assert.NotEmpty(Shared.Infrastructure.Loot.DropTableCatalog.Get(id));
    }

    [Fact]
    public void 스폰이_지목한_변종이_카탈로그에_존재한다_AC_G()
    {
        // 오타난 monsterId 는 Default 폴백(어빌리티 없음 = 공격 안 함)으로 조용히 떨어진다 → 여기서 잡는다.
        foreach (var mapId in new[] { "dungeon_01", "dungeon_02", "dungeon_03", "dungeon_04", "dungeon_05", "dungeon_e2e" })
        {
            foreach (var m in SpawnLayoutTable.Get(mapId).Monsters)
            {
                var def = MonsterCatalog.Get(m.MonsterId);
                Assert.False(string.IsNullOrEmpty(def.MonsterId),
                    $"{mapId}: '{m.MonsterId}' 가 monsters.json 에 없다(오타 → 공격 안 하는 유령 몬스터가 된다)");
            }
        }
    }

    [Fact]
    public void 상위_던전일수록_변종_구성이_강해진다_AC_G()
    {
        // 등급은 이제 monsterId 로 드러난다 — 스폰 필드를 볼 필요가 없다.
        Func<string, int> BossCount = mapId => SpawnLayoutTable.Get(mapId).Monsters
            .Count(m => MonsterCatalog.Get(m.MonsterId).Tier == MonsterTier.Boss);

        Assert.Equal(0, BossCount("dungeon_01"));  // 입문에 보스가 있으면 진행 곡선이 무너진다
        Assert.Equal(1, BossCount("dungeon_02"));
        Assert.Equal(2, BossCount("dungeon_05"));  // 최상급 = 보스 러시

        // 최상급은 잡몹이 없다(전원 엘리트 이상).
        Assert.DoesNotContain(SpawnLayoutTable.Get("dungeon_05").Monsters,
            m => MonsterCatalog.Get(m.MonsterId).Tier == MonsterTier.Normal);
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
