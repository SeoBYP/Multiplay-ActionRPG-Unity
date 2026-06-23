namespace GameServer.Domain.Entities.Quest;

/// <summary>
/// 퀘스트 *정의*(정적 기획데이터). 진행/수주 상태(UserQuest)와 분리 — 정의는 DB가 아니라 코드 카탈로그(ItemCatalog 동형).
/// </summary>
/// <param name="QuestId">카탈로그 키(예 "quest_slime_hunt").</param>
/// <param name="Name">표시 이름.</param>
/// <param name="Description">표시 설명.</param>
/// <param name="ObjectiveType">목표 종류.</param>
/// <param name="TargetId">목표 대상(KillMonster=monsterId, CollectItem=itemId 등).</param>
/// <param name="RequiredCount">완료에 필요한 수.</param>
/// <param name="Reward">완료 보상(exp/gold/item 조합).</param>
public sealed record QuestDef(
    string QuestId,
    string Name,
    string Description,
    QuestObjectiveType ObjectiveType,
    string TargetId,
    int RequiredCount,
    QuestReward Reward);

/// <summary>퀘스트 완료 보상(조합). 0/빈 값은 해당 보상 없음.</summary>
/// <param name="Exp">경험치(Progression).</param>
/// <param name="Gold">골드(Wallet).</param>
/// <param name="ItemId">지급 아이템(Inventory). null/빈 문자열이면 아이템 없음.</param>
/// <param name="ItemQty">지급 아이템 수량.</param>
public sealed record QuestReward(long Exp, long Gold, string? ItemId, int ItemQty);
