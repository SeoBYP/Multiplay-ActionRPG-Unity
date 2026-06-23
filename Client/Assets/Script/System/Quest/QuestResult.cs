namespace Game.System.Quest
{
    public enum QuestResult
    {
        Success,
        Unauthorized,
        Failed,
    }

    /// <summary>퀘스트 목표 종류(클라 미러 — proto enum 은닉).</summary>
    public enum QuestObjectiveKind
    {
        KillMonster,
        CollectItem,
        TalkToNpc,
    }

    /// <summary>퀘스트 진행 4-상태(클라 미러). NotAccepted=미수주 / Completed=보상 수령 가능.</summary>
    public enum QuestProgressState
    {
        NotAccepted,
        Accepted,
        Completed,
        Claimed,
    }
}
