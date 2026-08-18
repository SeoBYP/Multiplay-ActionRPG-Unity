using System.Reflection;
using System.Text.Json;

namespace Shared.Infrastructure.Quests;

/// <summary>
/// 퀘스트 정의 카탈로그 — quests.json(임베디드 bake). 수주/진행 상태만 DB(UserQuest)에 영속한다.
/// ItemCatalogData·MonsterCatalog 와 동일 교리: 클라 SO 저작 → bake → 서버 임베디드 로드.
///
/// <see cref="All"/> 의 순서 = 저작 순서 = **클라 퀘스트 목록 표시 순서**(QuestService 가 그대로 순회). 정렬 금지.
/// 공개 API 는 구 `GameServer.Domain.Entities.Quest.QuestCatalog` 와 동일하다(호출부 무변경).
/// </summary>
public static class QuestCatalog
{
    private const string ResourceName = "Shared.Infrastructure.Quests.quests.json";

    private static readonly Lazy<QuestTables> Tables = new(LoadEmbedded);

    /// <summary>정의가 존재하는 questId 인지.</summary>
    public static bool Contains(string questId) => Tables.Value.ById.ContainsKey(questId);

    /// <summary>정의를 반환. 없으면 null.</summary>
    public static QuestDef? Get(string questId) => Tables.Value.ById.GetValueOrDefault(questId);

    /// <summary>전체 정의(저작 순서 = 표시 순서).</summary>
    public static IReadOnlyCollection<QuestDef> All => Tables.Value.Ordered;

    private static QuestTables LoadEmbedded()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' not found.");

        return Parse(stream);
    }

    /// <summary>JSON 스트림 → 퀘스트 정의. 단위 테스트가 합성 JSON 으로 호출할 수 있도록 공개한다.</summary>
    public static QuestTables Parse(Stream stream)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var file = JsonSerializer.Deserialize<QuestFile>(stream, options)
            ?? throw new InvalidOperationException("Failed to parse quests.json");

        var ordered = new List<QuestDef>(file.Quests.Count);
        foreach (var dto in file.Quests)
        {
            if (string.IsNullOrWhiteSpace(dto.QuestId))
                throw new InvalidOperationException("quests.json contains an entry with an empty questId");

            ordered.Add(new QuestDef(
                dto.QuestId,
                dto.Name,
                dto.Description,
                Enum.Parse<QuestObjectiveType>(dto.ObjectiveType, ignoreCase: true),
                dto.TargetId,
                dto.RequiredCount,
                // 빈 문자열 itemId = 아이템 보상 없음(구 카탈로그의 null 과 동치로 정규화).
                new QuestReward(
                    dto.Reward.Exp,
                    dto.Reward.Gold,
                    string.IsNullOrEmpty(dto.Reward.ItemId) ? null : dto.Reward.ItemId,
                    dto.Reward.ItemQty)));
        }

        return new QuestTables(ordered);
    }

    /// <summary>파싱 산출물. 조회는 Dictionary, 열거는 저작 순서를 유지하는 List.</summary>
    public sealed class QuestTables
    {
        internal QuestTables(IReadOnlyList<QuestDef> ordered)
        {
            Ordered = ordered;
            ById = ordered.ToDictionary(q => q.QuestId, StringComparer.Ordinal);
        }

        public IReadOnlyList<QuestDef> Ordered { get; }
        public IReadOnlyDictionary<string, QuestDef> ById { get; }
    }

    // ── JSON DTO (클라 QuestCatalogExporter 의 JsonUtility 출력 형식과 1:1) ──
    private sealed class QuestFile
    {
        public List<QuestDto> Quests { get; set; } = new();
    }

    private sealed class QuestDto
    {
        public string QuestId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string ObjectiveType { get; set; } = "KillMonster";
        public string TargetId { get; set; } = "";
        public int RequiredCount { get; set; } = 1;
        public RewardDto Reward { get; set; } = new();
    }

    private sealed class RewardDto
    {
        public long Exp { get; set; }
        public long Gold { get; set; }
        public string ItemId { get; set; } = "";
        public int ItemQty { get; set; }
    }
}
