namespace GameServer.Application.Domains.Quest.Interfaces;

/// <summary>
/// 퀘스트 도메인 서비스(4.4). 수주/진행/완료·보상. 진행은 서버 권위(킬 클레임 경로에서만 +1, 클라 보고 아님).
/// 보상은 Progression+Wallet+Inventory 조합(Shop 동형). 보상 수령은 Claimed 선마킹 후 지급(중복 차단).
/// </summary>
public interface IQuestService
{
    /// <summary>카탈로그 전체 × 유저 상태 병합 목록(미수주 포함).</summary>
    Task<List<QuestStateView>> GetQuestsAsync(long userId, CancellationToken ct = default);

    /// <summary>퀘스트 수주. 미존재·이미 수주면 실패(멱등).</summary>
    Task<QuestAcceptResult> AcceptAsync(long userId, string questId, CancellationToken ct = default);

    /// <summary>
    /// 몬스터 처치 보고(서버 내부 — 킬 클레임 경로에서 호출). 해당 monsterId 를 목표로 하는
    /// Accepted·미완료 KillMonster 퀘스트들의 진행을 +1. 진행된 퀘스트 수 반환.
    /// </summary>
    Task<int> ReportKillAsync(long userId, string monsterId, CancellationToken ct = default);

    /// <summary>
    /// NPC 대화 보고(클라 대화 시작 시 호출). 해당 npcId 를 목표로 하는 Accepted·미완료 TalkToNpc 퀘스트 진행 +1.
    /// 잘못된 npcId 는 매칭 0(무진행). RequiredCount=1 + 진행 상한으로 반복 대화 멱등(파밍 불가). 진행된 퀘스트 수 반환.
    /// </summary>
    Task<int> ReportTalkAsync(long userId, string npcId, CancellationToken ct = default);

    /// <summary>완료 퀘스트 보상 수령. 미완료·미수주·이미수령이면 실패. 성공 시 보상 지급.</summary>
    Task<QuestClaimResult> ClaimRewardAsync(long userId, string questId, CancellationToken ct = default);
}
