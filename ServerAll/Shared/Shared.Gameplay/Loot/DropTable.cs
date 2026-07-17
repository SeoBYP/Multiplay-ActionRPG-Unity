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
        /// <param name="chanceMultiplier">
        /// 확률 배율(AC-E4) — 등급(Elite/Boss)이 잘 떨구게 한다. 1.0 = 기본. 결과 확률은 1.0 으로 clamp.
        /// </param>
        /// <param name="quantityMultiplier">
        /// 수량 배율(AC-E4) — 레벨이 오르면 보상 감각도 커진다. 1.0 = 기본.
        /// <para><b>가변 수량 아이템에만 적용</b>한다(<c>MaxQty &gt; 1</c>, 예: gold 10~30).
        /// 장비처럼 <c>1~1</c> 인 것에 배율을 걸면 검이 2자루 떨어진다 — 수량이 아니라 확률로 다뤄야 할 것들이다.</para>
        /// </param>
        /// <remarks>
        /// 배율을 <b>인자로 받는다</b> — 레벨·등급 계산은 <c>Shared.Infrastructure.MonsterLevelScaling</c> 이
        /// 플레이어 곡선을 참조해 하는데, 이 어셈블리(Shared.Gameplay)는 그 아래라 부를 수 없다.
        /// 순수 함수로 남겨 호출부가 숫자를 넣는다 — <c>StatCombatMath</c>(순수) + <c>MonsterLevelScaling</c>(테이블) 과 같은 관계.
        /// </remarks>
        public static List<DropResult> Roll(
            IReadOnlyList<DropEntry> entries,
            Random rng,
            double chanceMultiplier = 1.0,
            double quantityMultiplier = 1.0)
        {
            var results = new List<DropResult>();
            if (entries == null)
                return results;

            foreach (var entry in entries)
            {
                double chance = entry.Chance * chanceMultiplier;
                if (chance > 1.0) chance = 1.0;   // 확정 드롭을 넘어설 수는 없다

                if (rng.NextDouble() >= chance)
                    continue;

                int qty = entry.MinQty >= entry.MaxQty
                    ? entry.MinQty
                    : rng.Next(entry.MinQty, entry.MaxQty + 1);

                // 가변 수량(gold 등)만 레벨 스케일. 장비(1~1)는 그대로 1개.
                if (entry.MaxQty > 1 && quantityMultiplier != 1.0)
                    qty = Math.Max(1, (int)Math.Round(qty * quantityMultiplier));

                if (qty > 0)
                    results.Add(new DropResult(entry.ItemId, qty));
            }
            return results;
        }
    }
}
