using System.Collections.Concurrent;
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

    private static readonly Lazy<IReadOnlyDictionary<string, MapSpawnLayout>> Layouts = new(Load);

    /// <summary>
    /// mapId 의 레이아웃을 반환한다. 알 수 없는 mapId 면 예외(데이터 누락은 조용히 넘기지 않는다).
    /// </summary>
    public static MapSpawnLayout Get(string mapId)
    {
        if (Layouts.Value.TryGetValue(mapId, out var layout))
            return layout;

        throw new KeyNotFoundException($"Spawn layout for mapId '{mapId}' not found in spawn-layouts.json");
    }

    private static IReadOnlyDictionary<string, MapSpawnLayout> Load()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' not found.");

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var file = JsonSerializer.Deserialize<SpawnLayoutFile>(stream, options)
            ?? throw new InvalidOperationException("Failed to parse spawn-layouts.json");

        var result = new Dictionary<string, MapSpawnLayout>();
        foreach (var map in file.Maps)
        {
            var points = map.Points
                .Select(p => new SpawnPoint(p.X, p.Y, p.Z, p.RotY))
                .ToList();
            result[map.MapId] = new MapSpawnLayout(map.MapId, points);
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
        public List<PointDto> Points { get; set; } = new();
    }

    private sealed class PointDto
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float RotY { get; set; }
    }
}
