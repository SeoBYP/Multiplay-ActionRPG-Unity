using System;
using System.Collections.Generic;

namespace Shared.Gameplay
{
    /// <summary>
    /// 한 드랍 후보 — itemId 가 Chance 확률로 [MinQty, MaxQty] 수량 떨어진다.
    /// itemId 는 GameServer ItemCatalog 와 문자열로 정렬(드리프트는 grant 시 GameServer 가 검증·실패처리).
    /// </summary>
    public readonly struct DropEntry
    {
        public string ItemId { get; }
        public double Chance { get; }
        public int MinQty { get; }
        public int MaxQty { get; }

        public DropEntry(string itemId, double chance, int minQty, int maxQty)
        {
            ItemId = itemId;
            Chance = chance;
            MinQty = minQty;
            MaxQty = maxQty;
        }
    }

    /// <summary>roll 결과 한 항목(어떤 아이템 몇 개).</summary>
    public readonly struct DropResult
    {
        public string ItemId { get; }
        public int Qty { get; }

        public DropResult(string itemId, int qty)
        {
            ItemId = itemId;
            Qty = qty;
        }
    }

    /// <summary>
    /// 드랍 roll 순수 로직 — 데이터(어떤 몬스터가 무엇을 떨구나)는 외부(SO/JSON)에서 주입받는다.
    /// 서버(던전, drop-tables.json)와 클라(Main, ScriptableObject)가 같은 이 함수로 굴려 결과가 일관된다.
    /// rng 주입으로 테스트에서 결정론 검증 가능(런타임은 Random.Shared).
    /// </summary>
    public static class DropTableRoll
    {
        /// <summary>
        /// 각 후보를 독립 확률로 판정하고, 적중 시 [MinQty, MaxQty] 수량을 뽑는다.
        /// MinQty==MaxQty 면 Next 를 호출하지 않고 고정 수량.
        /// </summary>
        public static List<DropResult> Roll(IReadOnlyList<DropEntry> entries, Random rng)
        {
            var results = new List<DropResult>();
            if (entries == null)
                return results;

            foreach (var entry in entries)
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
}
