using System.Numerics;
using System.Reflection;
using System.Text.Json;
using Script.System.GamePlayAbilitySystem;

namespace Shared.Infrastructure.Abilities;

/// <summary>
/// abilityId / networkId → <see cref="AbilityDef"/> (게임플레이 판정 데이터). AC-B 단일 저작의 서버 측 조회.
/// 데이터 진실원 = 클라 `AbilityDefinition` SO 저작 → `AbilityCatalogExporter` bake → 임베디드 abilities.json
/// (DropTable/Monster/Skill 과 동일 교리, gas-architecture §2.5 / 설계 = ability-so-authoring.md).
///
/// 서버(UnityEngine 0)가 읽어 발동 게이트(쿨다운·마나)·hitbox·데미지·on-hit 를 권위 판정한다.
/// <b>Cue(애니 트리거)는 이 JSON 에 없다</b> — 연출은 클라 전용(gas §2: "서버는 Cue 를 하나도 모른다").
///
/// ※ B1 단계에서는 로드·조회만 제공하고 아무도 사용하지 않는다(기존 skills.json 경로 그대로).
///   B2 에서 `CombatHandler.ResolveSkill` 하드코딩 switch 를 이 카탈로그 조회로 대체한다.
/// </summary>
public static class AbilityCatalog
{
    private const string ResourceName = "Shared.Infrastructure.Abilities.abilities.json";

    private static readonly Lazy<Tables> Data = new(LoadEmbedded);

    /// <summary>abilityId 의 정의. 미등록이면 null.</summary>
    public static AbilityDef? Get(string? id)
        => id != null && Data.Value.ById.TryGetValue(id, out var a) ? a : null;

    /// <summary>패킷 networkId(S_AbilityActivated.SkillId / C_Attack.SkillId) 의 정의. 미등록이면 null.</summary>
    public static AbilityDef? Get(int networkId)
        => Data.Value.ByNetworkId.TryGetValue(networkId, out var a) ? a : null;

    /// <summary>등록된 전체 어빌리티(디버그·검증용).</summary>
    public static IReadOnlyList<AbilityDef> All => Data.Value.All;

    private static Tables LoadEmbedded()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' not found.");

        return Parse(stream);
    }

    /// <summary>JSON 스트림 → 조회 테이블. 단위 테스트가 합성 JSON 으로 호출할 수 있도록 공개.</summary>
    public static Tables Parse(Stream stream)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var file = JsonSerializer.Deserialize<AbilityFile>(stream, options)
            ?? throw new InvalidOperationException("Failed to parse abilities.json");

        var byId = new Dictionary<string, AbilityDef>();
        var byNetworkId = new Dictionary<int, AbilityDef>();

        foreach (var a in file.Abilities)
        {
            var shape = Enum.TryParse<EHitboxShape>(a.HitboxShape, ignoreCase: true, out var parsed)
                ? parsed
                : EHitboxShape.Box;

            var hitbox = new HitboxSpec(
                shape,
                new Vector3(a.OffsetX, a.OffsetY, a.OffsetZ),
                new Vector3(a.HalfX, a.HalfY, a.HalfZ));

            var timeline = new SkillTimeline(
                a.Id, a.StartupMs, a.ActiveMs, a.RecoveryMs, a.CooldownMs,
                hitbox, a.OnHitEffectIds ?? new List<string>(), a.ManaCost,
                a.ComboChainMs, a.ComboWindowMs);

            var def = new AbilityDef(a.Id, a.NetworkId, timeline, a.BaseDamage, a.ActivationRange);

            byId[a.Id] = def;
            byNetworkId[a.NetworkId] = def; // networkId 유일성은 Exporter 가 저작 시점에 검증한다.
        }

        return new Tables(byId, byNetworkId, byId.Values.ToList());
    }

    /// <summary>파싱 결과 조회 테이블(두 키 + 전체 목록).</summary>
    public sealed record Tables(
        IReadOnlyDictionary<string, AbilityDef> ById,
        IReadOnlyDictionary<int, AbilityDef> ByNetworkId,
        IReadOnlyList<AbilityDef> All);

    private sealed class AbilityFile
    {
        public List<AbilityDto> Abilities { get; set; } = new();
    }

    private sealed class AbilityDto
    {
        public string Id { get; set; } = "";
        public int NetworkId { get; set; }
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
        public int BaseDamage { get; set; }
        public float ActivationRange { get; set; }
        public List<string> OnHitEffectIds { get; set; } = new();
        public int ComboChainMs { get; set; }
        public int ComboWindowMs { get; set; }
    }
}

/// <summary>
/// 한 어빌리티의 서버 측 정의 — 게임플레이 전부. Cue 없음(클라 전용).
/// <paramref name="Timeline"/> = 기존 <see cref="SkillTimeline"/>(쿨다운·hitbox·on-hit·콤보) 재사용.
/// </summary>
/// <param name="BaseDamage">스탯 스케일 전 base 데미지(AC-B 안B: 플레이어·몬스터 공용 데미지 출처).</param>
/// <param name="ActivationRange">발동 가능 사거리(m). 몬스터 AI 의 "지금 쏠 수 있나" 판정.</param>
public sealed record AbilityDef(
    string Id,
    int NetworkId,
    SkillTimeline Timeline,
    int BaseDamage,
    float ActivationRange);
