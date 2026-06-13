using System.Reflection;
using System.Text.Json;
using Script.System.GamePlayAbilitySystem;

namespace Shared.Infrastructure.Consumables;

/// <summary>
/// consumable-effects.json(임베디드 리소스)을 1회 로드해 소모품 회복 효과를 GameplayEffectDefinition 으로 제공한다.
/// DropTableCatalog 와 동일 컨벤션(클라 SO 저작 → JSON bake → 서버 임베디드 로드).
///
/// 효과 수치 진실원 = 클라 `ConsumableCatalog` SO. Export 툴(Tools/Consumables/Export)이 이 JSON 으로 bake 한다.
/// 서버는 UnityEngine 의존이 0이라 SO 를 못 읽으므로 이 임베디드 JSON 을 읽는다(클라는 SO 직접).
/// effectId == itemId 규칙: 소비 통지의 EffectId 가 곧 itemId 라 별도 매핑이 필요 없다.
///
/// 위치가 Shared.Infrastructure 인 이유: JSON 파싱(System.Text.Json)은 인프라 관심사이고,
/// 클라는 SO 를 직접 읽으므로 파서를 공유할 필요가 없다(Shared.Gameplay 는 순수 유지).
/// </summary>
public static class ConsumableEffectCatalog
{
    private const string ResourceName = "Shared.Infrastructure.Consumables.consumable-effects.json";

    /// <summary>임베디드 JSON → 소모품 GameplayEffectDefinition 목록(즉발 회복). CombatEffectCatalog 가 Register 로 흡수.</summary>
    public static IReadOnlyList<GameplayEffectDefinition> LoadDefinitions()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' not found.");

        return Parse(stream);
    }

    /// <summary>JSON 스트림 → 소모품 정의. 단위 테스트가 합성 JSON 으로 호출할 수 있도록 공개한다.</summary>
    public static IReadOnlyList<GameplayEffectDefinition> Parse(Stream stream)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var file = JsonSerializer.Deserialize<ConsumableFile>(stream, options)
            ?? throw new InvalidOperationException("Failed to parse consumable-effects.json");

        var defs = new List<GameplayEffectDefinition>();
        foreach (var c in file.Consumables)
        {
            var mods = (c.Modifiers ?? new List<ModDto>())
                .Select(m => GameplayAttributeModifier.Create(
                    Enum.Parse<EGameplayAttribute>(m.Stat, ignoreCase: true), m.Amount, EModifierType.Additive))
                .ToList();

            // 소모품 회복 = 즉발(Instant). 버프 아이콘 없음(category cosmetic). Duration 버프 소모품은 후속(YAGNI).
            defs.Add(new GameplayEffectDefinition(
                id: c.ItemId,
                category: EEffectCategory.AttackPower,
                policy: EDurationPolicy.Instant,
                durationMs: 0,
                modifiers: mods));
        }
        return defs;
    }

    private sealed class ConsumableFile
    {
        public List<ConsumableDto> Consumables { get; set; } = new();
    }

    private sealed class ConsumableDto
    {
        public string ItemId { get; set; } = "";
        public List<ModDto> Modifiers { get; set; } = new();
    }

    private sealed class ModDto
    {
        public string Stat { get; set; } = "Health";
        public int Amount { get; set; }
    }
}
