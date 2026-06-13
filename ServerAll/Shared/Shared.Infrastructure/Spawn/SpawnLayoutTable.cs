using System.IO;
using System.Reflection;
using System.Text.Json;

namespace Shared.Infrastructure.Spawn;

/// <summary>
/// spawn-layouts.json(임베디드 리소스)을 1회 로드해 맵별 레이아웃을 제공한다.
/// 클라이언트는 같은 JSON 정본을 Unity Resources 로 미러링해 동일 데이터를 읽는다.
/// </summary>
public static class SpawnLayoutTable
{
    private const string ResourceName = "Shared.Infrastructure.Spawn.spawn-layouts.json";

    private static readonly Lazy<IReadOnlyDictionary<string, MapSpawnLayout>> Layouts = new(LoadEmbedded);

    /// <summary>
    /// mapId 의 레이아웃을 반환한다. 알 수 없는 mapId 면 예외(데이터 누락은 조용히 넘기지 않는다).
    /// </summary>
    public static MapSpawnLayout Get(string mapId)
    {
        if (Layouts.Value.TryGetValue(mapId, out var layout))
            return layout;

        throw new KeyNotFoundException($"Spawn layout for mapId '{mapId}' not found in spawn-layouts.json");
    }

    private static IReadOnlyDictionary<string, MapSpawnLayout> LoadEmbedded()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' not found.");

        return Parse(stream);
    }

    /// <summary>
    /// JSON 스트림 → 맵별 레이아웃. 단위 테스트가 합성 JSON 으로 호출할 수 있도록 공개한다.
    /// bounds 가 없으면 MapBounds.Unbounded, monsters/patrol 이 없으면 빈 목록으로 채운다.
    /// </summary>
    public static IReadOnlyDictionary<string, MapSpawnLayout> Parse(Stream stream)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var file = JsonSerializer.Deserialize<SpawnLayoutFile>(stream, options)
            ?? throw new InvalidOperationException("Failed to parse spawn-layouts.json");

        var result = new Dictionary<string, MapSpawnLayout>();
        foreach (var map in file.Maps)
        {
            var points = map.Points
                .Select(p => new SpawnPoint(p.X, p.Y, p.Z, p.RotY))
                .ToList();

            var bounds = map.Bounds is { } b
                ? new MapBounds(b.CenterX, b.CenterZ, b.SizeX, b.SizeZ)
                : MapBounds.Unbounded;

            var monsters = (map.Monsters ?? new List<MonsterDto>())
                .Select(m => new MonsterSpawnDef(
                    m.MonsterId,
                    m.X, m.Y, m.Z, m.RotY,
                    m.Count, m.Wave,
                    (m.Patrol ?? new List<PatrolDto>())
                        .Select(p => new PatrolPoint(p.X, p.Z))
                        .ToList(),
                    m.SlotId, m.RespawnCooldownMs))
                .ToList();

            result[map.MapId] = new MapSpawnLayout(map.MapId, points, bounds, monsters, map.ExpReward);
        }
        return result;
    }

    private sealed class SpawnLayoutFile
    {
        public List<MapEntry> Maps { get; set; } = new();
    }

    private sealed class MapEntry
    {
        public string MapId { get; set; } = "";
        public BoundsDto? Bounds { get; set; }
        public List<PointDto> Points { get; set; } = new();
        public List<MonsterDto> Monsters { get; set; } = new();

        /// <summary>던전 클리어 시 참가자 전원에게 지급할 경험치. 누락 시 0(보상 없음).</summary>
        public long ExpReward { get; set; }
    }

    private sealed class PointDto
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float RotY { get; set; }
    }

    private sealed class BoundsDto
    {
        public float CenterX { get; set; }
        public float CenterZ { get; set; }
        public float SizeX { get; set; }
        public float SizeZ { get; set; }
    }

    private sealed class MonsterDto
    {
        public string MonsterId { get; set; } = "";
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float RotY { get; set; }
        public int Count { get; set; } = 1;
        public int Wave { get; set; }
        public List<PatrolDto> Patrol { get; set; } = new();

        /// <summary>Main B-lite 클레임 키(슬롯 안정 식별자). 던전 미사용(기본 0). main-spawn-claim.md.</summary>
        public int SlotId { get; set; }

        /// <summary>Main B-lite 재청구 쿨다운(ms) = 파밍률 상한. 던전 미사용(기본 0).</summary>
        public int RespawnCooldownMs { get; set; }
    }

    private sealed class PatrolDto
    {
        public float X { get; set; }
        public float Z { get; set; }
    }
}
