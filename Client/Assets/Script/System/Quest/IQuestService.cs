using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.System.Quest
{
    /// <summary>
    /// 퀘스트 서비스. 조회/수주/보상수령(서버 권위). 진행(ReportKill)은 서버 내부라 클라 RPC 없음.
    /// proto(GameServer.Grpc.Quest)는 여기서 숨기고 도메인 DTO(QuestData)만 노출.
    /// </summary>
    public interface IQuestService
    {
        UniTask<(QuestResult Result, IReadOnlyList<QuestData> Quests)> GetQuestsAsync(CancellationToken ct = default);

        /// <summary>퀘스트 수주.</summary>
        UniTask<QuestResult> AcceptAsync(string questId, CancellationToken ct = default);

        /// <summary>완료 퀘스트 보상 수령.</summary>
        UniTask<QuestResult> ClaimRewardAsync(string questId, CancellationToken ct = default);

        /// <summary>NPC 대화 보고(대화 시작 시) — 해당 npcId 목표 TalkToNpc 퀘스트 진행(서버 권위·멱등).</summary>
        UniTask<QuestResult> ReportTalkAsync(string npcId, CancellationToken ct = default);
    }
}
