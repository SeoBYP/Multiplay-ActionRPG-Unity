using GameServer.Application.Domains.Inventory.Interfaces;
using GameServer.Application.Domains.Progression.Interfaces;
using GameServer.Application.Domains.Quest.Interfaces;
using GameServer.Application.Domains.Wallet.Interfaces;
using GameServer.Domain.Entities.Quest;
using Shared.Infrastructure.Quests;

namespace GameServer.Application.Domains.Quest;

/// <summary>
/// 퀘스트 서비스 구현. 정의=QuestCatalog(정적), 상태=IQuestRepository(DB). 진행은 서버 권위(ReportKill 내부 호출만).
/// 보상은 Progression+Wallet+Inventory 조합(Shop 동형). 중복 수령은 Claimed 선마킹으로 차단.
/// </summary>
public sealed class QuestService(
    IQuestRepository repository,
    IProgressionService progression,
    IWalletService wallet,
    IInventoryService inventory) : IQuestService
{
    public async Task<List<QuestStateView>> GetQuestsAsync(long userId, CancellationToken ct = default)
    {
        var rows = await repository.GetAllForUserAsync(userId, ct);
        var byId = rows.ToDictionary(r => r.QuestId);

        var views = new List<QuestStateView>(QuestCatalog.All.Count);
        foreach (var def in QuestCatalog.All)
        {
            byId.TryGetValue(def.QuestId, out var row);
            var status = Resolve(row, def.RequiredCount);
            views.Add(new QuestStateView(def, status, row?.Progress ?? 0));
        }
        return views;
    }

    public async Task<QuestAcceptResult> AcceptAsync(long userId, string questId, CancellationToken ct = default)
    {
        if (!QuestCatalog.Contains(questId))
            return QuestAcceptResult.Fail("unknown quest");

        var existing = await repository.GetAsync(userId, questId, ct);
        if (existing is not null)
            return QuestAcceptResult.Fail("already accepted");

        await repository.UpsertAsync(UserQuest.Create(userId, questId), ct);
        return QuestAcceptResult.Ok();
    }

    public Task<int> ReportKillAsync(long userId, string monsterId, CancellationToken ct = default)
        => AdvanceMatchingAsync(userId, QuestObjectiveType.KillMonster, monsterId, ct);

    public Task<int> ReportTalkAsync(long userId, string npcId, CancellationToken ct = default)
        => AdvanceMatchingAsync(userId, QuestObjectiveType.TalkToNpc, npcId, ct);

    /// <summary>주어진 목표타입·대상(targetId)을 가진 Accepted·미완료 퀘스트들의 진행을 +1. 진행 수 반환.</summary>
    private async Task<int> AdvanceMatchingAsync(long userId, QuestObjectiveType objective, string targetId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(targetId))
            return 0;

        var rows = await repository.GetAllForUserAsync(userId, ct);
        int advanced = 0;
        foreach (var row in rows)
        {
            var def = QuestCatalog.Get(row.QuestId);
            if (def is null || def.ObjectiveType != objective || def.TargetId != targetId)
                continue;

            if (row.AddProgress(1, def.RequiredCount))
            {
                await repository.UpsertAsync(row, ct);
                advanced++;
            }
        }
        return advanced;
    }

    public async Task<QuestClaimResult> ClaimRewardAsync(long userId, string questId, CancellationToken ct = default)
    {
        var def = QuestCatalog.Get(questId);
        if (def is null)
            return QuestClaimResult.Fail("unknown quest");

        var row = await repository.GetAsync(userId, questId, ct);
        if (row is null)
            return QuestClaimResult.Fail("not accepted");
        if (row.Status == QuestStatus.Claimed)
            return QuestClaimResult.Fail("already claimed");
        if (row.Progress < def.RequiredCount)
            return QuestClaimResult.Fail("not completed");

        // 중복 수령 차단: Claimed 선마킹·영속 후 지급(지급 실패해도 재수령 불가 — 설계 결정 plan §4.4).
        if (!row.Claim(def.RequiredCount))
            return QuestClaimResult.Fail("not completed");
        await repository.UpsertAsync(row, ct);

        // 보상 조합 지급.
        if (def.Reward.Exp > 0)
            await progression.AddExpAsync(userId, def.Reward.Exp, ct);
        if (def.Reward.Gold > 0)
            await wallet.AddAsync(userId, def.Reward.Gold, ct);
        if (!string.IsNullOrEmpty(def.Reward.ItemId) && def.Reward.ItemQty > 0)
            await inventory.GrantItemAsync(userId, def.Reward.ItemId!, def.Reward.ItemQty, ct);

        return QuestClaimResult.Ok(def.Reward);
    }

    /// <summary>행+필요수로 4-상태 파생. 행 없음=미수주, Claimed=수령완료, 완료치 도달=Completed, 그 외 Accepted.</summary>
    private static QuestProgressStatus Resolve(UserQuest? row, int required)
    {
        if (row is null)
            return QuestProgressStatus.NotAccepted;
        if (row.Status == QuestStatus.Claimed)
            return QuestProgressStatus.Claimed;
        return row.Progress >= required ? QuestProgressStatus.Completed : QuestProgressStatus.Accepted;
    }
}
