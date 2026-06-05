using System.IO;
using System.Text;
using Shared.Infrastructure.Spawn;

namespace Server.Tests.Room;

/// <summary>
/// M3 증분②c: spawn-layouts.json 의 monsters/patrol/bounds 파싱 + MapBounds clamp 계약.
/// 파싱 테스트는 합성 JSON 으로 검증해 shipped 데이터 재저작에 영향받지 않는다.
/// </summary>
public class MonsterSpawnLayoutTests
{
    private static IReadOnlyDictionary<string, MapSpawnLayout> ParseJson(string json)
    {
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return SpawnLayoutTable.Parse(ms);
    }

    [Fact]
    public void Parse_몬스터와_패트롤_경계를_읽는다()
    {
        const string json = """
        {
          "maps": [{
            "mapId": "dungeon_test",
            "bounds": { "centerX": 1, "centerZ": 2, "sizeX": 40, "sizeZ": 30 },
            "points": [{ "x": 0, "y": 0, "z": 0, "rotY": 0 }],
            "monsters": [{
              "monsterId": "slime", "x": 5, "y": 0, "z": 7, "rotY": 90,
              "count": 2, "wave": 1,
              "patrol": [ { "x": 5, "z": 7 }, { "x": 9, "z": 7 } ]
            }]
          }]
        }
        """;

        var layout = ParseJson(json)["dungeon_test"];

        Assert.Equal(1f, layout.Bounds.CenterX);
        Assert.Equal(2f, layout.Bounds.CenterZ);
        Assert.Equal(40f, layout.Bounds.SizeX);
        Assert.Equal(30f, layout.Bounds.SizeZ);
        Assert.True(layout.Bounds.HasArea);

        var m = Assert.Single(layout.Monsters);
        Assert.Equal("slime", m.MonsterId);
        Assert.Equal(5f, m.X);
        Assert.Equal(7f, m.Z);
        Assert.Equal(90f, m.RotY);
        Assert.Equal(2, m.Count);
        Assert.Equal(1, m.Wave);
        Assert.Equal(2, m.Patrol.Count);
        Assert.Equal(5f, m.Patrol[0].X);
        Assert.Equal(7f, m.Patrol[0].Z);
        Assert.Equal(9f, m.Patrol[1].X);
    }

    [Fact]
    public void Parse_bounds_없으면_Unbounded이고_monsters는_빈목록()
    {
        const string json = """
        { "maps": [{ "mapId": "m", "points": [{ "x":0,"y":0,"z":0,"rotY":0 }] }] }
        """;

        var layout = ParseJson(json)["m"];

        Assert.False(layout.Bounds.HasArea); // 무경계
        Assert.Empty(layout.Monsters);
    }

    [Fact]
    public void MapBounds_경계_밖_좌표를_안으로_clamp한다()
    {
        var b = new MapBounds(0f, 0f, 10f, 10f); // x,z ∈ [-5, 5]

        Assert.Equal((5f, 5f), b.Clamp(100f, 100f));
        Assert.Equal((-5f, -5f), b.Clamp(-100f, -100f));
        Assert.Equal((2f, 3f), b.Clamp(2f, 3f)); // 안쪽은 그대로
        Assert.True(b.Contains(2f, 3f));
        Assert.False(b.Contains(100f, 0f));
    }

    [Fact]
    public void MapBounds_무경계는_clamp하지_않는다()
    {
        var b = MapBounds.Unbounded;

        Assert.Equal((100f, -100f), b.Clamp(100f, -100f));
        Assert.True(b.Contains(99999f, -99999f));
    }

    [Fact]
    public void 임베디드_dungeon_01은_경계와_몬스터_패트롤을_포함한다()
    {
        var layout = SpawnLayoutTable.Get(MapIds.Dungeon01);

        Assert.True(layout.Bounds.HasArea);
        Assert.NotEmpty(layout.Monsters);
        Assert.NotEmpty(layout.Monsters[0].Patrol);
    }
}
