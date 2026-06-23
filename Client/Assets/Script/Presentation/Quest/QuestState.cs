using System;
using System.Collections.Generic;

namespace Game.Presentation.Quest
{
    /// <summary>퀘스트 화면 상태(불변).</summary>
    public sealed class QuestState
    {
        public static readonly QuestState Initial = new(Array.Empty<QuestEntryModel>(), isLoading: false, error: null);

        public IReadOnlyList<QuestEntryModel> Quests { get; }
        public bool IsLoading { get; }
        public string Error { get; }

        public QuestState(IReadOnlyList<QuestEntryModel> quests, bool isLoading, string error)
        {
            Quests = quests;
            IsLoading = isLoading;
            Error = error;
        }

        public QuestState With(IReadOnlyList<QuestEntryModel> quests = null, bool? isLoading = null,
            string error = null, bool clearError = false)
            => new(quests ?? Quests, isLoading ?? IsLoading, clearError ? null : (error ?? Error));
    }
}
