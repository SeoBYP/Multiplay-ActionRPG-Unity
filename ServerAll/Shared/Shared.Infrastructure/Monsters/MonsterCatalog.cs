using System.Reflection;
using System.Text.Json;

namespace Shared.Infrastructure.Monsters;

/// <summary>
/// 몬스터 타입별 정의(스탯 + 보상). 스폰 위치 데이터(MonsterSpawnDef)와 분리된 "몬스터가 무엇인가"의 단일 저작 소스.
/// 클라 `MonsterCatalogDefinition` SO 저작 → bake → 임베디드 monsters.json (DropTable/LevelTable 과 동일 교리).
///
/// 양 서버가 읽는다:
///   - SocketServer(던전 시뮬): MaxHp·이동·공격 스탯 (Server.Monster.MonsterCatalog 가 위임).
///   - GameServer(Main 킬): ExpReward (MainSpawnClaimService 가 slot.monsterId 로 조회해 적립).
/// </summary>
public static class MonsterCatalog
{
    private const string ResourceName = "Shared.Infrastructure.Monsters.monsters.json";

    /// <summary>미등록 타입 폴백 — 데이터 누락에도 동작(보상 0, 약한 기본 스탯).</summary>
    public static readonly MonsterDef Default = new(
        MonsterId: "", MaxHp: 30, MoveSpeed: 2.0f, AggroRange: 6f, AbilityIds: Array.Empty<string>(), ExpReward: 0);

    private static readonly Lazy<IReadOnlyDictionary<string, MonsterDef>> Table = new(LoadEmbedded);

    /// <summary>monsterId 의 정의. 미등록이면 <see cref="Default"/>.</summary>
    public static MonsterDef Get(string? monsterId)
        => monsterId != null && Table.Value.TryGetValue(monsterId, out var def) ? def : Default;

    private static IReadOnlyDictionary<string, MonsterDef> LoadEmbedded()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' not found.");

        return Parse(stream);
    }

    /// <summary>JSON 스트림 → monsterId 별 정의. 단위 테스트가 합성 JSON 으로 호출할 수 있도록 공개한다.</summary>
    public static IReadOnlyDictionary<string, MonsterDef> Parse(Stream stream)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var file = JsonSerializer.Deserialize<MonsterFile>(stream, options)
            ?? throw new InvalidOperationException("Failed to parse monsters.json");

        var result = new Dictionary<string, MonsterDef>();
        foreach (var m in file.Monsters)
        {
            result[m.MonsterId] = new MonsterDef(
                m.MonsterId, m.MaxHp, m.MoveSpeed, m.AggroRange,
                m.AbilityIds ?? new List<string>(), m.ExpReward);
        }
        return result;
    }

    private sealed class MonsterFile
    {
        public List<MonsterDto> Monsters { get; set; } = new();
    }

    private sealed class MonsterDto
    {
        public string MonsterId { get; set; } = "";
        public int MaxHp { get; set; } = 30;
        public float MoveSpeed { get; set; } = 2.0f;
        public float AggroRange { get; set; } = 6f;
        public List<string> AbilityIds { get; set; } = new();
        public int ExpReward { get; set; }
    }
}

/// <summary>
/// 한 몬스터 타입의 정의 — "무엇인가"(체력·이동·시야) + 보상 + **어빌리티 목록**(AC-B B4).
/// 공격 사거리·쿨다운·데미지·CC 는 전부 <see cref="Shared.Infrastructure.Abilities.AbilityCatalog"/> 의 어빌리티가 갖는다.
/// 스폰 위치는 MonsterSpawnDef 가 따로 가짐.
/// </summary>
/// <param name="AbilityIds">쓸 수 있는 어빌리티 id. **우선순위 = 순서**(서버 AI 가 사거리·쿨다운 만족 첫 어빌리티 발동).</param>
public sealed record MonsterDef(
    string MonsterId,
    int MaxHp,
    float MoveSpeed,
    float AggroRange,
    IReadOnlyList<string> AbilityIds,
    int ExpReward);
