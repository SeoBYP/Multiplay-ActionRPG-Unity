using System.Numerics;
using System.Reflection;
using System.Text.Json;
using Script.System.GamePlayAbilitySystem;

namespace Shared.Infrastructure.Skills;

/// <summary>
/// skillId → <see cref="SkillTimeline"/> (게임플레이 판정 데이터). 데이터 진실원 = 클라 `SkillDefinition` SO 저작 →
/// bake → 임베디드 skills.json (DropTable/Monster/Consumable 과 동일 교리, gas-architecture §2.5).
///
/// 서버(UnityEngine 0)가 읽어 발동 게이트(쿨다운)·hitbox·on-hit effect 를 권위 판정한다(CombatHandler).
/// SkillTimeline 은 Shared.Gameplay 순수 타입 — 연출(Cue/VFX) 미포함(클라 전용).
/// </summary>
public static class SkillCatalog
{
    private const string ResourceName = "Shared.Infrastructure.Skills.skills.json";

    private static readonly Lazy<IReadOnlyDictionary<string, SkillTimeline>> Table = new(LoadEmbedded);

    /// <summary>skillId 의 타임라인. 미등록이면 null.</summary>
    public static SkillTimeline? Get(string? id)
        => id != null && Table.Value.TryGetValue(id, out var s) ? s : null;

    private static IReadOnlyDictionary<string, SkillTimeline> LoadEmbedded()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' not found.");

        return Parse(stream);
    }

    /// <summary>JSON 스트림 → skillId 별 SkillTimeline. 단위 테스트가 합성 JSON 으로 호출할 수 있도록 공개.</summary>
    public static IReadOnlyDictionary<string, SkillTimeline> Parse(Stream stream)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var file = JsonSerializer.Deserialize<SkillFile>(stream, options)
            ?? throw new InvalidOperationException("Failed to parse skills.json");

        var result = new Dictionary<string, SkillTimeline>();
        foreach (var s in file.Skills)
        {
            var shape = Enum.TryParse<EHitboxShape>(s.HitboxShape, ignoreCase: true, out var parsed)
                ? parsed
                : EHitboxShape.Box;

            var hitbox = new HitboxSpec(
                shape,
                new Vector3(s.OffsetX, s.OffsetY, s.OffsetZ),
                new Vector3(s.HalfX, s.HalfY, s.HalfZ));

            result[s.Id] = new SkillTimeline(
                s.Id, s.StartupMs, s.ActiveMs, s.RecoveryMs, s.CooldownMs,
                hitbox, s.OnHitEffectIds ?? new List<string>(), s.ManaCost,
                s.ComboChainMs, s.ComboWindowMs);
        }
        return result;
    }

    private sealed class SkillFile
    {
        public List<SkillDto> Skills { get; set; } = new();
    }

    private sealed class SkillDto
    {
        public string Id { get; set; } = "";
        public int StartupMs { get; set; }
        public int ActiveMs { get; set; }
        public int RecoveryMs { get; set; }
        public int CooldownMs { get; set; }
        public int ManaCost { get; set; }
        public string HitboxShape { get; set; } = "Box";
        public float OffsetX { get; set; }
        public float OffsetY { get; set; }
        public float OffsetZ { get; set; }
        public float HalfX { get; set; }
        public float HalfY { get; set; }
        public float HalfZ { get; set; }
        public List<string> OnHitEffectIds { get; set; } = new();

        // 콤보 타이밍(서버·클라 공유 진실원). 0 = 콤보 아님. 구 JSON(필드 없음)도 0 으로 안전 로드.
        public int ComboChainMs { get; set; }
        public int ComboWindowMs { get; set; }
    }
}
