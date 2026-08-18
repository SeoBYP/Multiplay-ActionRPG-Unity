using GameServer.Domain.Entities.Quest;
using Shared.Infrastructure.Quests;

namespace GameServer.Application.Domains.Quest;

/// <summary>퀘스트 1건의 조회 뷰 = 정의 + 유저 상태(병합). GetQuests 가 카탈로그 전체 × UserQuest 로 만든다.</summary>
public sealed record QuestStateView(QuestDef Def, QuestProgressStatus Status, int Progress);

/// <summary>UI 표시용 4-상태. NotAccepted=행없음 / Completed=Accepted且Progress≥Required(보상 대기) 파생.</summary>
public enum QuestProgressStatus
{
    NotAccepted,
    Accepted,   // 진행 중(미완료)
    Completed,  // 완료(보상 수령 가능)
    Claimed,    // 보상 수령 완료
}

/// <summary>수주 결과.</summary>
public sealed record QuestAcceptResult(bool Success, string? Reason)
{
    public static QuestAcceptResult Ok() => new(true, null);
    public static QuestAcceptResult Fail(string reason) => new(false, reason);
}

/// <summary>보상 수령 결과(성공 시 지급된 보상 요약 포함).</summary>
public sealed record QuestClaimResult(bool Success, string? Reason, QuestReward? Reward)
{
    public static QuestClaimResult Ok(QuestReward reward) => new(true, null, reward);
    public static QuestClaimResult Fail(string reason) => new(false, reason, null);
}
