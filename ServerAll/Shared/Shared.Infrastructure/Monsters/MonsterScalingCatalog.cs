using System.Reflection;
using System.Text.Json;

namespace Shared.Infrastructure.Monsters;

/// <summary>
/// 한 등급의 배율. <b>HP 를 크게, 피해를 작게</b> 올리는 게 원칙 —
/// 피해를 크게 올리면 즉사가 되고, HP 를 올리면 "오래 버티는 위협"이 된다(액션 RPG 관례).
/// </summary>
public sealed record MonsterTierDef(
    MonsterTier Tier,
    float HpMultiplier,
    float DamageMultiplier,
    float ExpMultiplier,
    float DropChanceMultiplier);

/// <summary>
/// 몬스터 등급 배율 테이블(AC-F2). 클라 <c>MonsterScalingDefinition</c> SO 저작 → bake → 임베디드 monster-scaling.json.
/// (MonsterCatalog/DropTable/LevelTable 과 동일 교리 — 밸런스 수치는 코드가 아니라 테이블에 있다.)
///
/// <para>이전엔 이 배율들이 <c>MonsterLevelScaling</c> 안에 <c>switch</c> 로 하드코딩돼 있어
/// 기획이 값을 바꾸려면 서버 코드를 고쳐야 했다.</para>
/// </summary>
public static class MonsterScalingCatalog
{
    private const string ResourceName = "Shared.Infrastructure.Monsters.monster-scaling.json";

    /// <summary>미등록 등급 폴백 = 배율 없음. 데이터 누락이 스탯 붕괴로 번지지 않게.</summary>
    public static readonly MonsterTierDef Default = new(MonsterTier.Normal, 1f, 1f, 1f, 1f);

    private static readonly Lazy<IReadOnlyDictionary<MonsterTier, MonsterTierDef>> Table = new(LoadEmbedded);

    /// <summary>등급의 배율. 미등록이면 <see cref="Default"/>(배율 1).</summary>
    public static MonsterTierDef Get(MonsterTier tier)
        => Table.Value.TryGetValue(tier, out var def) ? def : Default;

    public static IReadOnlyCollection<MonsterTierDef> All => (IReadOnlyCollection<MonsterTierDef>)Table.Value.Values;

    private static IReadOnlyDictionary<MonsterTier, MonsterTierDef> LoadEmbedded()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' not found.");
        return Parse(stream);
    }

    /// <summary>JSON 스트림 → 등급별 배율. 단위 테스트가 합성 JSON 으로 호출할 수 있도록 공개한다.</summary>
    public static IReadOnlyDictionary<MonsterTier, MonsterTierDef> Parse(Stream stream)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var file = JsonSerializer.Deserialize<ScalingFile>(stream, options)
            ?? throw new InvalidOperationException("Failed to parse monster-scaling.json");

        var result = new Dictionary<MonsterTier, MonsterTierDef>();
        foreach (var t in file.Tiers)
        {
            var tier = (MonsterTier)t.Tier;
            result[tier] = new MonsterTierDef(
                tier, t.HpMultiplier, t.DamageMultiplier, t.ExpMultiplier, t.DropChanceMultiplier);
        }
        return result;
    }

    private sealed class ScalingFile
    {
        public List<TierDto> Tiers { get; set; } = new();
    }

    private sealed class TierDto
    {
        /// <summary>0=Normal · 1=Elite · 2=Boss. 계약은 int — 클라 미러 enum 과 값으로 맞춘다.</summary>
        public int Tier { get; set; }
        public float HpMultiplier { get; set; } = 1f;
        public float DamageMultiplier { get; set; } = 1f;
        public float ExpMultiplier { get; set; } = 1f;
        public float DropChanceMultiplier { get; set; } = 1f;
    }
}
