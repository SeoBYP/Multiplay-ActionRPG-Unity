using GameServer.Application.Domains.Inventory.Interfaces;
using Microsoft.Extensions.Logging;
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
    /// NPC 대화 보고. 서버가 검증하는 것은 **"이 요청을 정상적으로 처리할 수 있는가"** 다:
    ///   ① 카탈로그에 이 npcId 를 대상으로 하는 TalkToNpc 퀘스트 정의가 있는가
    ///   ② 그 유저가 그 퀘스트를 수락했고 미완료인가 (AdvanceMatchingAsync 가 판정)
    ///
    /// ⚠ **근접("정말 그 NPC 앞에 갔는가")은 검증하지 않는다 — 이 구조에서는 불가능하다.**
    /// 서버는 NPC 위치를 모르고(NPC 는 씬 배치, 위치 카탈로그 없음), Main 씬은 소켓 미연결이라
    /// 플레이어 위치도 모른다. 근접 검증은 Main 을 서버 권위로 올려야 성립한다(cleanup-backlog F5).
    ///
    /// ①을 DB 조회 **앞**에 두는 이유: 카탈로그만 봐도 판정되는 요청이 저장소 왕복을 유발하지 않게.
    /// 실패는 예외가 아니라 0 이다 — 퀘스트 없는 NPC 와 대화하는 것은 정상 행동이다.
    /// </summary>
    public async Task<int> ReportTalkAsync(long userId, string npcId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(npcId))
            return 0;

        if (!HasTalkObjective(npcId))
        {
            // 클라는 퀘스트를 가진 NPC 에서만 호출한다 → 여기 오는 것 자체가 비정상 경로다(관측용 로그).
            logger.LogWarning("[Quest] ReportTalk 무시 — 대화 목표가 없는 npcId={NpcId} (user {UserId})", npcId, userId);
            return 0;
        }

        var advanced = await AdvanceMatchingAsync(userId, QuestObjectiveType.TalkToNpc, npcId, ct);
        if (advanced > 0)
            logger.LogInformation("[Quest] ReportTalk 진행 user={UserId} npc={NpcId} 퀘스트={Count}건", userId, npcId, advanced);

        return advanced;
    }

    /// <summary>이 npcId 를 대상으로 하는 TalkToNpc 퀘스트 정의가 카탈로그에 있는가.</summary>
    private static bool HasTalkObjective(string npcId)
    {
        foreach (var def in QuestCatalog.All)
            if (def.ObjectiveType == QuestObjectiveType.TalkToNpc && def.TargetId == npcId)
                return true;
        return false;
    }

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
        if (def.Reward.ItemId != 0 && def.Reward.ItemQty > 0)
            await inventory.GrantItemAsync(userId, def.Reward.ItemId, def.Reward.ItemQty, ct);

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
