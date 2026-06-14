using System.Reflection;
using System.Text.Json;

namespace Shared.Infrastructure.Progression;

/// <summary>
/// level-table.json(임베디드 리소스)을 1회 로드해 레벨별 진행 데이터(다음 레벨까지 Exp + 스탯)를 제공한다.
/// DropTableCatalog 와 동일 컨벤션(클라 SO 저작 → JSON bake → 서버 임베디드 로드).
///
/// 데이터 진실원 = 클라 `LevelTableDefinition` SO. Export 툴(Tools/Progression/Export)이 이 JSON 으로 bake 한다.
/// 스탯은 레벨에서 결정론 룩업 — DB 에는 Level/Exp 만 영속하고 스탯은 항상 여기서 파생한다(단일소스).
///
/// 위치가 Shared.Infrastructure 인 이유: JSON 파싱(System.Text.Json)은 인프라 관심사이고,
/// 클라는 SO 를 직접 읽으므로 파서를 공유할 필요가 없다(Shared.Gameplay 는 순수 유지).
/// </summary>
public static class LevelTable
{
    private const string ResourceName = "Shared.Infrastructure.Progression.level-table.json";

    private static readonly Lazy<IReadOnlyDictionary<int, LevelRow>> Rows = new(LoadEmbedded);
    private static readonly Lazy<int> MaxLevelLazy = new(() => Rows.Value.Keys.Max());

    /// <summary>테이블의 최고 레벨(현재 60). 레벨업 루프 상한.</summary>
    public static int MaxLevel => MaxLevelLazy.Value;

    /// <summary>
    /// 현재 레벨 → 다음 레벨까지 필요한 Exp. 최고 레벨(이상)이면 long.MaxValue 를 반환해
    /// 레벨업 루프가 절대 통과하지 못하게 한다(만렙 고정). 레벨이 범위를 벗어나면 경계로 clamp.
    /// </summary>
    public static long ExpToNext(int level)
    {
        if (level >= MaxLevel)
            return long.MaxValue;
        var row = Lookup(level);
        // 데이터상 만렙 행의 expToNext 는 0(다음 없음)이지만, 위 가드로 도달하지 않는다.
        return row.ExpToNext > 0 ? row.ExpToNext : long.MaxValue;
    }

    /// <summary>레벨의 스탯(룩업). 범위를 벗어나면 1~MaxLevel 로 clamp.</summary>
    public static LevelStats StatsAt(int level)
    {
        var row = Lookup(level);
        return new LevelStats(
            row.MaxHealth, row.MaxMana, row.AttackPower, row.Defense,
            row.Strength, row.Dexterity, row.Intelligence);
    }

    private static LevelRow Lookup(int level)
    {
        int clamped = Math.Clamp(level, 1, MaxLevel);
        return Rows.Value.TryGetValue(clamped, out var row)
            ? row
            : throw new InvalidOperationException($"Level {clamped} missing from level-table.json");
    }

    private static IReadOnlyDictionary<int, LevelRow> LoadEmbedded()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' not found.");

        return Parse(stream);
    }

    /// <summary>JSON 스트림 → 레벨별 행. 단위 테스트가 합성 JSON 으로 호출할 수 있도록 공개한다.</summary>
    public static IReadOnlyDictionary<int, LevelRow> Parse(Stream stream)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var file = JsonSerializer.Deserialize<LevelTableFile>(stream, options)
            ?? throw new InvalidOperationException("Failed to parse level-table.json");

        var result = new Dictionary<int, LevelRow>();
        foreach (var dto in file.Levels)
        {
            result[dto.Level] = new LevelRow(
                dto.Level, dto.ExpToNext, dto.MaxHealth, dto.MaxMana, dto.AttackPower,
                dto.Defense, dto.Strength, dto.Dexterity, dto.Intelligence);
        }

        if (result.Count == 0)
            throw new InvalidOperationException("level-table.json has no levels");

        return result;
    }

    public readonly record struct LevelRow(
        int Level, long ExpToNext, int MaxHealth, int MaxMana, int AttackPower,
        int Defense, int Strength, int Dexterity, int Intelligence);

    private sealed class LevelTableFile
    {
        public List<LevelDto> Levels { get; set; } = new();
    }

    private sealed class LevelDto
    {
        public int Level { get; set; }
        public long ExpToNext { get; set; }
        public int MaxHealth { get; set; }
        public int MaxMana { get; set; }
        public int AttackPower { get; set; }
        public int Defense { get; set; }
        public int Strength { get; set; }
        public int Dexterity { get; set; }
        public int Intelligence { get; set; }
    }
}

/// <summary>레벨에서 파생된 base 스탯 묶음(2.4 스탯합산의 입력).</summary>
public readonly record struct LevelStats(
    int MaxHealth, int MaxMana, int AttackPower, int Defense,
    int Strength, int Dexterity, int Intelligence);
