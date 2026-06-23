namespace Game.Presentation.Dialogue
{
    /// <summary>대화 View → Model 의도. View는 Accept(Intent)만 호출한다.</summary>
    public abstract class DialogueIntent
    {
        /// <summary>선택지 선택(현재 노드의 노출 선택지 인덱스).</summary>
        public sealed class SelectChoice : DialogueIntent
        {
            public readonly int Index;
            public SelectChoice(int index) => Index = index;
        }

        /// <summary>대화 닫기(X 또는 EndDialogue).</summary>
        public sealed class Close : DialogueIntent
        {
            public static readonly Close Instance = new();
            private Close() { }
        }
    }
}
