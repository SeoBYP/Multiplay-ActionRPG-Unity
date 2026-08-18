namespace Shared.Infrastructure.Quests;

/// <summary>
/// 퀘스트 목표 종류. proto enum `gameserver.quest.v1.QuestObjective` 와 **정수값 1:1**.
/// MVP=KillMonster 만 진행 훅 존재. 나머지는 확장 여지(정의/표시 가능, 진행 훅은 후속).
/// </summary>
public enum QuestObjectiveType
{
    KillMonster = 0,  // TargetId = monsterId, 킬 클레임(ClaimExpAsync)에서 +1
    CollectItem = 1,  // TargetId = itemId (진행 훅 후속 — GrantItemAsync 합류 시)
    TalkToNpc = 2,    // TargetId = npcId (4.5 NPC 합류 시)
}

/// <summary>
/// 퀘스트 *정의*(정적 기획데이터). 진행/수주 상태(UserQuest)와 분리 — 정의는 DB 가 아니라 bake 카탈로그.
/// Name·Description 은 **서버가 proto(QuestInfo)로 클라에 보내므로** bake 에 포함한다(아이템 표시 필드와 다름).
/// </summary>
/// <param name="QuestId">카탈로그 키(예 "quest_slime_hunt").</param>
/// <param name="Name">표시 이름.</param>
/// <param name="Description">표시 설명.</param>
/// <param name="ObjectiveType">목표 종류.</param>
/// <param name="TargetId">목표 대상(KillMonster=monsterId, CollectItem=itemId, TalkToNpc=npcId).</param>
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
/// <param name="ItemId">지급 아이템의 numericId(Inventory). 0 이면 아이템 보상 없음.</param>
/// <param name="ItemQty">지급 아이템 수량.</param>
public sealed record QuestReward(long Exp, long Gold, int ItemId, int ItemQty);
