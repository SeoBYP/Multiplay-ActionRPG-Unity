using GameServer.Application.Domains.Inventory.Interfaces;
using Microsoft.Extensions.Logging;
using GameServer.Application.Domains.Progression.Interfaces;
using GameServer.Application.Domains.Quest.Interfaces;
using GameServer.Application.Domains.Wallet.Interfaces;
using GameServer.Domain.Entities.Quest;
using Shared.Infrastructure.Quests;

namespace GameServer.Application.Domains.Quest;

/// <summary>
/// 퀘스트 서비스 구현. 정의=QuestCatalog(정적), 상태=IQuestRepository(DB). 중복 수령은 Claimed 선마킹으로 차단.
/// </summary>
public sealed class QuestService(
    IQuestRepository repository,
    IProgressionService progression,
    IWalletService wallet,
    IInventoryService inventory,
    ILogger<QuestService> logger) : IQuestService
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

    /// <summary>
    /// NPC 대화 보고. 검증은 ① 카탈로그에 해당 npcId 의 TalkToNpc 정의가 있는가(DB 조회 앞), ② 수락했고 미완료인가(AdvanceMatchingAsync).
    /// 근접("정말 그 NPC 앞인가")은 검증하지 않는다 — 서버가 NPC·플레이어 위치를 모른다.
    /// 실패는 예외가 아니라 0 이다.
    /// </summary>
    public async Task<int> ReportTalkAsync(long userId, string npcId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(npcId))
            return 0;

        if (!HasTalkObjective(npcId))
        {
            // 클라는 퀘스트를 가진 NPC 에서만 호출하므로 여기 오는 것 자체가 비정상 경로
            logger.LogWarning("[Quest] ReportTalk 무시 — 대화 목표가 없는 npcId={NpcId} (user {UserId})", npcId, userId);
            return 0;
        }

        var advanced = await AdvanceMatchingAsync(userId, QuestObjectiveType.TalkToNpc, npcId, ct);
        if (advanced > 0)
            logger.LogInformation("[Quest] ReportTalk 진행 user={UserId} npc={NpcId} 퀘스트={Count}건", userId, npcId, advanced);

        return advanced;
    }

    private static bool HasTalkObjective(string npcId)
    {
        foreach (var def in QuestCatalog.All)
            if (def.ObjectiveType == QuestObjectiveType.TalkToNpc && def.TargetId == npcId)
                return true;
        return false;
    }

    /// <summary>목표타입·targetId 가 일치하는 Accepted·미완료 퀘스트를 +1. 진행된 수 반환.</summary>
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

        // Claimed 선마킹·영속 후 지급 — 지급이 실패해도 재수령은 불가하다(중복 지급 차단 우선)
        if (!row.Claim(def.RequiredCount))
            return QuestClaimResult.Fail("not completed");
        await repository.UpsertAsync(row, ct);

        if (def.Reward.Exp > 0)
            await progression.AddExpAsync(userId, def.Reward.Exp, ct);
        if (def.Reward.Gold > 0)
            await wallet.AddAsync(userId, def.Reward.Gold, ct);
        if (def.Reward.ItemId != 0 && def.Reward.ItemQty > 0)
            await inventory.GrantItemAsync(userId, def.Reward.ItemId, def.Reward.ItemQty, ct);

        return QuestClaimResult.Ok(def.Reward);
    }

    /// <summary>행+필요수로 상태 파생: 행 없음=미수주, Claimed=수령완료, 완료치 도달=Completed, 그 외 Accepted.</summary>
    private static QuestProgressStatus Resolve(UserQuest? row, int required)
    {
        if (row is null)
            return QuestProgressStatus.NotAccepted;
        if (row.Status == QuestStatus.Claimed)
            return QuestProgressStatus.Claimed;
        return row.Progress >= required ? QuestProgressStatus.Completed : QuestProgressStatus.Accepted;
    }
}
