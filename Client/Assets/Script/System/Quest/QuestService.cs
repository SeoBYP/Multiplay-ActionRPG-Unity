using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Network.Https.Interfaces;
using GameServer.Grpc.Quest;
using UnityEngine;
using GrpcObjective = GameServer.Grpc.Quest.QuestObjective;
using GrpcStatus = GameServer.Grpc.Quest.QuestProgressStatus;

namespace Game.System.Quest
{
    /// <summary>퀘스트 서비스. gRPC 호출 → 도메인 DTO 변환. WalletService 동형(에러 catch → Failed).</summary>
    public sealed class QuestService : IQuestService
    {
        private readonly IQuestGrpcService _grpc;

        public QuestService(IQuestGrpcService grpc)
        {
            _grpc = grpc;
        }

        public async UniTask<(QuestResult Result, IReadOnlyList<QuestData> Quests)> GetQuestsAsync(CancellationToken ct = default)
        {
            try
            {
                var res = await _grpc.GetQuestsAsync(new GetQuestsRequest(), ct);
                if (!res.Result.Success)
                    return (QuestResult.Failed, Array.Empty<QuestData>());

                var list = new List<QuestData>(res.Quests.Count);
                foreach (var q in res.Quests)
                    list.Add(Map(q));
                return (QuestResult.Success, list);
            }
            catch (Exception e)
            {
                Debug.LogError($"[QuestService] GetQuests 예외: {e.Message}");
                return (QuestResult.Failed, Array.Empty<QuestData>());
            }
        }

        public async UniTask<QuestResult> AcceptAsync(string questId, CancellationToken ct = default)
        {
            try
            {
                var res = await _grpc.AcceptQuestAsync(new AcceptQuestRequest { QuestId = questId }, ct);
                return res.Result.Success ? QuestResult.Success : QuestResult.Failed;
            }
            catch (Exception e)
            {
                Debug.LogError($"[QuestService] AcceptQuest 예외: {e.Message}");
                return QuestResult.Failed;
            }
        }

        public async UniTask<QuestResult> ClaimRewardAsync(string questId, CancellationToken ct = default)
        {
            try
            {
                var res = await _grpc.ClaimQuestRewardAsync(new ClaimQuestRewardRequest { QuestId = questId }, ct);
                return res.Result.Success ? QuestResult.Success : QuestResult.Failed;
            }
            catch (Exception e)
            {
                Debug.LogError($"[QuestService] ClaimQuestReward 예외: {e.Message}");
                return QuestResult.Failed;
            }
        }

        public async UniTask<QuestResult> ReportTalkAsync(string npcId, CancellationToken ct = default)
        {
            try
            {
                var res = await _grpc.ReportTalkAsync(new ReportTalkRequest { NpcId = npcId }, ct);
                return res.Result.Success ? QuestResult.Success : QuestResult.Failed;
            }
            catch (Exception e)
            {
                Debug.LogError($"[QuestService] ReportTalk 예외: {e.Message}");
                return QuestResult.Failed;
            }
        }

        private static QuestData Map(QuestInfo q) => new(
            q.QuestId, q.Name, q.Description,
            MapObjective(q.ObjectiveType), q.TargetId, q.RequiredCount, q.CurrentProgress,
            MapStatus(q.Status),
            new QuestRewardData(q.Reward?.Exp ?? 0, q.Reward?.Gold ?? 0, q.Reward?.ItemId ?? string.Empty, q.Reward?.ItemQty ?? 0));

        private static QuestObjectiveKind MapObjective(GrpcObjective o) => o switch
        {
            GrpcObjective.CollectItem => QuestObjectiveKind.CollectItem,
            GrpcObjective.TalkToNpc => QuestObjectiveKind.TalkToNpc,
            _ => QuestObjectiveKind.KillMonster,
        };

        private static QuestProgressState MapStatus(GrpcStatus s) => s switch
        {
            GrpcStatus.Accepted => QuestProgressState.Accepted,
            GrpcStatus.Completed => QuestProgressState.Completed,
            GrpcStatus.Claimed => QuestProgressState.Claimed,
            _ => QuestProgressState.NotAccepted,
        };
    }
}
