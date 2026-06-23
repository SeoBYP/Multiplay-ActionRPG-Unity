namespace GameServer.Domain.Entities.Quest;

/// <summary>퀘스트 목표 종류. MVP=KillMonster 만 진행 훅 존재. 나머지는 확장 여지(정의/표시 가능, 진행 훅은 후속).</summary>
public enum QuestObjectiveType
{
    KillMonster,  // TargetId = monsterId, 킬 클레임(ClaimExpAsync)에서 +1
    CollectItem,  // TargetId = itemId (진행 훅 후속 — GrantItemAsync 합류 시)
    TalkToNpc,    // TargetId = npcId (4.5 NPC 합류 시)
}
