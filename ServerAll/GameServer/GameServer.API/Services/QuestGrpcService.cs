using GameServer.API.Extension;
using GameServer.API.Extensions;
using GameServer.Application.Common;
using GameServer.Application.Domains.Quest;
using GameServer.Application.Domains.Quest.Interfaces;
using GameServer.Grpc.Quest;
using Grpc.Core;
using DomainObjective = Shared.Infrastructure.Quests.QuestObjectiveType;
using AppStatus = GameServer.Application.Domains.Quest.QuestProgressStatus;
using GrpcStatus = GameServer.Grpc.Quest.QuestProgressStatus;
using QuestGrpc = GameServer.Grpc.Quest.QuestService;

namespace GameServer.API.Services;

/// <summary>
/// 퀘스트 gRPC(4.4). 조회/수주/보상수령. 진행(ReportKill)은 서버 내부(킬 클레임)에서만 — 클라 RPC 없음(치팅 차단).
/// 도메인 ↔ proto 매핑만 담당, 로직은 IQuestService.
/// </summary>
public class QuestGrpcService(
    IQuestService questService,
    ILogger<QuestGrpcService> logger) : QuestGrpc.QuestServiceBase
{
    public override async Task<GetQuestsResponse> GetQuests(GetQuestsRequest request, ServerCallContext context)
    {
        var userId = context.GetUserId();
        if (userId is null)
            return new GetQuestsResponse { Result = ResultExtensions.CreateUnauthorizedGrpcResult() };

        var views = await questService.GetQuestsAsync(userId.Value, context.CancellationToken);

        var response = new GetQuestsResponse { Result = Result.Success().ToGrpcResult() };
        foreach (var v in views)
            response.Quests.Add(ToInfo(v));

        logger.LogInformation("GetQuests succeeded for user {UserId}: {Count} quests", userId, views.Count);
        return response;
    }

    public override async Task<AcceptQuestResponse> AcceptQuest(AcceptQuestRequest request, ServerCallContext context)
    {
        var userId = context.GetUserId();
        if (userId is null)
            return new AcceptQuestResponse { Result = ResultExtensions.CreateUnauthorizedGrpcResult() };

        var result = await questService.AcceptAsync(userId.Value, request.QuestId, context.CancellationToken);
        return new AcceptQuestResponse
        {
            Result = result.Success
                ? Result.Success().ToGrpcResult()
                : Result.Failure(ErrorCodes.InvalidRequest, result.Reason ?? "accept failed").ToGrpcResult(),
        };
    }

    public override async Task<ClaimQuestRewardResponse> ClaimQuestReward(ClaimQuestRewardRequest request, ServerCallContext context)
    {
        var userId = context.GetUserId();
        if (userId is null)
            return new ClaimQuestRewardResponse { Result = ResultExtensions.CreateUnauthorizedGrpcResult() };

        var result = await questService.ClaimRewardAsync(userId.Value, request.QuestId, context.CancellationToken);
        if (!result.Success)
            return new ClaimQuestRewardResponse { Result = Result.Failure(ErrorCodes.InvalidRequest, result.Reason ?? "claim failed").ToGrpcResult() };

        return new ClaimQuestRewardResponse
        {
            Result = Result.Success().ToGrpcResult(),
            Reward = ToReward(result.Reward!),
        };
    }

    public override async Task<ReportTalkResponse> ReportTalk(ReportTalkRequest request, ServerCallContext context)
    {
        var userId = context.GetUserId();
        if (userId is null)
            return new ReportTalkResponse { Result = ResultExtensions.CreateUnauthorizedGrpcResult() };

        var advanced = await questService.ReportTalkAsync(userId.Value, request.NpcId, context.CancellationToken);
        return new ReportTalkResponse { Result = Result.Success().ToGrpcResult(), Advanced = advanced };
    }

    private static QuestInfo ToInfo(QuestStateView v) => new()
    {
        QuestId = v.Def.QuestId,
        Name = v.Def.Name,
        Description = v.Def.Description,
        ObjectiveType = ToObjective(v.Def.ObjectiveType),
        TargetId = v.Def.TargetId,
        RequiredCount = v.Def.RequiredCount,
        CurrentProgress = v.Progress,
        Status = ToStatus(v.Status),
        Reward = ToReward(v.Def.Reward),
    };

    private static QuestReward ToReward(Shared.Infrastructure.Quests.QuestReward r) => new()
    {
        Exp = r.Exp,
        Gold = r.Gold,
        ItemId = r.ItemId,   // 0 = 아이템 보상 없음
        ItemQty = r.ItemQty,
    };

    private static QuestObjective ToObjective(DomainObjective o) => o switch
    {
        DomainObjective.CollectItem => QuestObjective.CollectItem,
        DomainObjective.TalkToNpc => QuestObjective.TalkToNpc,
        _ => QuestObjective.KillMonster,
    };

    private static GrpcStatus ToStatus(AppStatus s) => s switch
    {
        AppStatus.Accepted => GrpcStatus.Accepted,
        AppStatus.Completed => GrpcStatus.Completed,
        AppStatus.Claimed => GrpcStatus.Claimed,
        _ => GrpcStatus.NotAccepted,
    };
}
