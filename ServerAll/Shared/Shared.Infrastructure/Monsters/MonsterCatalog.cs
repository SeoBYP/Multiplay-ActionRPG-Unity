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
        MonsterId: "", MaxHp: 30, MoveSpeed: 2.0f, AggroRange: 6f,
        AttackRange: 1.2f, AttackCooldownMs: 1500f, AttackDamage: 5, ExpReward: 0, OnHitEffectId: "");

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
                m.AttackRange, m.AttackCooldownMs, m.AttackDamage, m.ExpReward, m.OnHitEffectId ?? "");
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
        public float AttackRange { get; set; } = 1.2f;
        public float AttackCooldownMs { get; set; } = 1500f;
        public int AttackDamage { get; set; } = 5;
        public int ExpReward { get; set; }
        public string OnHitEffectId { get; set; } = ""; // CC: 적중 시 부여할 효과 id(빈 문자열=없음).
    }
}

/// <summary>한 몬스터 타입의 정의 — 시뮬 스탯 + 보상(exp). 스폰 위치는 MonsterSpawnDef 가 따로 가짐.</summary>
public sealed record MonsterDef(
    string MonsterId,
    int MaxHp,
    float MoveSpeed,
    float AggroRange,
    float AttackRange,
    float AttackCooldownMs,
    int AttackDamage,
    int ExpReward,
    string OnHitEffectId = ""); // CC: 적중 시 부여할 효과 id(빈 문자열=없음). 던전 TickMonsters 가 S_ApplyEffect 로 브로드캐스트.
