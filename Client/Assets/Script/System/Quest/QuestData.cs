namespace Game.System.Quest
{
    /// <summary>퀘스트 1건(System DTO — proto 은닉). Presentation 이 View-facing 모델로 변환.</summary>
    public sealed class QuestData
    {
        public string QuestId { get; }
        public string Name { get; }
        public string Description { get; }
        public QuestObjectiveKind Objective { get; }
        public string TargetId { get; }
        public int RequiredCount { get; }
        public int CurrentProgress { get; }
        public QuestProgressState Status { get; }
        public QuestRewardData Reward { get; }

        public QuestData(string questId, string name, string description, QuestObjectiveKind objective,
            string targetId, int requiredCount, int currentProgress, QuestProgressState status, QuestRewardData reward)
        {
            QuestId = questId;
            Name = name;
            Description = description;
            Objective = objective;
            TargetId = targetId;
            RequiredCount = requiredCount;
            CurrentProgress = currentProgress;
            Status = status;
            Reward = reward;
        }
    }

    /// <summary>퀘스트 보상(System DTO). ItemId 빈 문자열이면 아이템 없음.</summary>
    public readonly struct QuestRewardData
    {
        public readonly long Exp;
        public readonly long Gold;
        public readonly string ItemId;
        public readonly int ItemQty;

        public QuestRewardData(long exp, long gold, string itemId, int itemQty)
        {
            Exp = exp;
            Gold = gold;
            ItemId = itemId;
            ItemQty = itemQty;
        }
    }
}
