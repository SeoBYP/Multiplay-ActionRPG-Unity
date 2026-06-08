namespace Server.Loot;

/// <summary>
/// 한 드랍 후보 — itemId 가 Chance 확률로 [MinQty, MaxQty] 수량 떨어진다.
/// itemId 는 GameServer ItemCatalog 와 문자열로 정렬(드리프트는 grant 시 GameServer 가 검증·실패처리).
/// </summary>
public sealed record DropEntry(string ItemId, double Chance, int MinQty, int MaxQty);

/// <summary>roll 결과 한 항목(어떤 아이템 몇 개).</summary>
public readonly record struct DropResult(string ItemId, int Qty);

/// <summary>
/// monsterId → 드랍 후보 목록. 정적 기획데이터(코드 카탈로그) — MonsterCatalog·spawn-layouts 와 동일 컨벤션.
/// roll 은 itemId 문자열 + 확률만 다룬다(이름·아이콘·MaxStack 같은 *정의* 는 GameServer 소유).
/// 위치가 SocketServer 인 이유: roll 은 바닥 스폰과 한 몸이고 전투 틱 루프 안에서 일어난다(loot-drop.md §1.1).
/// </summary>
public static class DropTable
{
    private static readonly IReadOnlyList<DropEntry> Empty = Array.Empty<DropEntry>();

    private static readonly Dictionary<string, List<DropEntry>> Table = new()
    {
        ["slime"] = new List<DropEntry>
        {
            // potion_hp_small 은 보장 드랍(Chance 1.0): 흔한 몹은 항상 소량 회복 포션을 떨군다.
            // → 처치→드랍 체인이 결정적이라 풀 루트 E2E(사냥→드랍→줍기→인벤토리)가 안정적으로 통과한다.
            new("potion_hp_small", 1.0, 1, 1),
            new("gold_pouch",      0.2, 1, 3),
        },
    };

    /// <summary>몬스터 타입의 드랍 후보. 미등록이면 빈 목록(드랍 없음).</summary>
    public static IReadOnlyList<DropEntry> Get(string? monsterId)
        => monsterId != null && Table.TryGetValue(monsterId, out var list) ? list : Empty;

    /// <summary>
    /// 몬스터 타입의 드랍을 굴린다. 각 후보를 독립 확률로 판정하고, 적중 시 [MinQty, MaxQty] 수량을 뽑는다.
    /// rng 를 주입받아 테스트에서 결정론적으로 검증 가능(서버는 Random.Shared 사용).
    /// </summary>
    public static List<DropResult> Roll(string? monsterId, Random rng)
    {
        var results = new List<DropResult>();
        foreach (var entry in Get(monsterId))
        {
            if (rng.NextDouble() >= entry.Chance)
                continue;

            int qty = entry.MinQty >= entry.MaxQty
                ? entry.MinQty
                : rng.Next(entry.MinQty, entry.MaxQty + 1);

            if (qty > 0)
                results.Add(new DropResult(entry.ItemId, qty));
        }
        return results;
    }
}
