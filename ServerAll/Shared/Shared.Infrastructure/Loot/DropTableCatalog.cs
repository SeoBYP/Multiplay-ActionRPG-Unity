using System.IO;
using System.Reflection;
using System.Text.Json;
using Shared.Gameplay;

namespace Shared.Infrastructure.Loot;

/// <summary>
/// drop-tables.json(임베디드 리소스)을 1회 로드해 monsterId 별 드랍 후보를 제공한다.
/// 정적 기획데이터(코드 카탈로그) — spawn-layouts 와 동일 컨벤션(SO 저작 → JSON bake → 양쪽 로드).
/// roll 로직은 Shared.Gameplay.DropTableRoll(순수)에 위임 — 서버(던전)·클라(Main)가 동일 결과.
/// 위치가 Shared.Infrastructure 인 이유: JSON 파싱(System.Text.Json)은 인프라 관심사이고,
/// 클라는 ScriptableObject 를 직접 읽으므로 JSON 파서를 공유할 필요가 없다(Shared.Gameplay 는 순수 유지).
/// </summary>
public static class DropTableCatalog
{
    private const string ResourceName = "Shared.Infrastructure.Loot.drop-tables.json";

    private static readonly System.Lazy<IReadOnlyDictionary<string, IReadOnlyList<DropEntry>>> Tables = new(LoadEmbedded);

    private static readonly IReadOnlyList<DropEntry> Empty = new List<DropEntry>();

    /// <summary>몬스터 타입의 드랍 후보. 미등록이면 빈 목록(드랍 없음).</summary>
    public static IReadOnlyList<DropEntry> Get(string? monsterId)
        => monsterId != null && Tables.Value.TryGetValue(monsterId, out var list) ? list : Empty;

    /// <summary>몬스터 타입의 드랍을 굴린다(Get → DropTableRoll). 런타임은 Random.Shared 주입.</summary>
    public static List<DropResult> Roll(string? monsterId, System.Random rng)
        => DropTableRoll.Roll(Get(monsterId), rng);

    private static IReadOnlyDictionary<string, IReadOnlyList<DropEntry>> LoadEmbedded()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new System.InvalidOperationException($"Embedded resource '{ResourceName}' not found.");

        return Parse(stream);
    }

    /// <summary>
    /// JSON 스트림 → monsterId 별 드랍 후보. 단위 테스트가 합성 JSON 으로 호출할 수 있도록 공개한다.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<DropEntry>> Parse(Stream stream)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var file = JsonSerializer.Deserialize<DropTableFile>(stream, options)
            ?? throw new System.InvalidOperationException("Failed to parse drop-tables.json");

        var result = new Dictionary<string, IReadOnlyList<DropEntry>>();
        foreach (var table in file.Tables)
        {
            var entries = (table.Drops ?? new List<DropDto>())
                .Select(d => new DropEntry(d.ItemId, d.Chance, d.MinQty, d.MaxQty))
                .ToList();
            result[table.MonsterId] = entries;
        }
        return result;
    }

    private sealed class DropTableFile
    {
        public List<TableEntry> Tables { get; set; } = new();
    }

    private sealed class TableEntry
    {
        public string MonsterId { get; set; } = "";
        public List<DropDto> Drops { get; set; } = new();
    }

    private sealed class DropDto
    {
        public string ItemId { get; set; } = "";
        public double Chance { get; set; }
        public int MinQty { get; set; } = 1;
        public int MaxQty { get; set; } = 1;
    }
}
