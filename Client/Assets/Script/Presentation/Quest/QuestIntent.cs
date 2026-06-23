namespace Game.Presentation.Quest
{
    /// <summary>퀘스트 View → Model 의도. View는 Accept(Intent)만 호출한다.</summary>
    public abstract class QuestIntent
    {
        /// <summary>목록 새로고침(열 때).</summary>
        public sealed class Refresh : QuestIntent
        {
            public static readonly Refresh Instance = new();
            private Refresh() { }
        }

        /// <summary>퀘스트 수주.</summary>
        public sealed class Accept : QuestIntent
        {
            public readonly string QuestId;
            public Accept(string questId) => QuestId = questId;
        }

        /// <summary>완료 퀘스트 보상 수령.</summary>
        public sealed class Claim : QuestIntent
        {
            public readonly string QuestId;
            public Claim(string questId) => QuestId = questId;
        }
    }
}
