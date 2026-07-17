using Microsoft.Extensions.Logging.Abstractions;
using Shared.Infrastructure.Messages;
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
    public void 저작된_레이아웃은_아직_전부_L1이다_E2_동작보존()
    {
        // E2 는 필드와 해석 경로만 넣는다 — 던전 대역 저작은 별도 결정(E3/E5).
        // 지금 값이 바뀌면 이 증분이 "동작 보존"이 아니게 된다.
        foreach (var mapId in new[] { "dungeon_01", "dungeon_02", "dungeon_e2e" })
        {
            var layout = SpawnLayoutTable.Get(mapId);
            Assert.Equal(0, layout.MonsterLevel);
            Assert.All(layout.Monsters, m => Assert.Equal(0, m.Level));
            Assert.All(layout.Monsters, m => Assert.Equal(MonsterTier.Normal, m.Tier));
        }
    }
}
