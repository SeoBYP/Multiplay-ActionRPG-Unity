using System.Collections.Generic;
using Game.System.Quest;

namespace Game.Presentation.Quest
{
    /// <summary>퀘스트 1건의 View-facing 모델. GUI는 이 타입만 본다(System DTO·proto 비노출).</summary>
    public sealed class QuestEntryModel
    {
        public string QuestId { get; }
        public string Name { get; }
        public string Description { get; }
        public int RequiredCount { get; }
        public int CurrentProgress { get; }
        public QuestProgressState Status { get; }
        public string ProgressText { get; }   // "2 / 3" 또는 완료/수령
        public string RewardText { get; }      // "보상: 경험치 50 골드 100"
        public string ConditionText { get; }   // 목표 조건 한 줄(예: "creepy_demon 처치 2/3")
        public bool ConditionMet { get; }       // 조건 충족(진행≥필요)
        public IReadOnlyList<string> RewardLines { get; } // 보상 항목별 문자열(reward 슬롯 분리 표시용)

        /// <summary>수주 버튼 노출(미수주).</summary>
        public bool CanAccept => Status == QuestProgressState.NotAccepted;
        /// <summary>보상받기 버튼 노출(완료·미수령).</summary>
        public bool CanClaim => Status == QuestProgressState.Completed;
        /// <summary>이미 수령(종료).</summary>
        public bool IsClaimed => Status == QuestProgressState.Claimed;

        public QuestEntryModel(string questId, string name, string description, int requiredCount,
            int currentProgress, QuestProgressState status, string progressText, string rewardText,
            string conditionText, bool conditionMet, IReadOnlyList<string> rewardLines)
        {
            QuestId = questId;
            Name = name;
            Description = description;
            RequiredCount = requiredCount;
            CurrentProgress = currentProgress;
            Status = status;
            ProgressText = progressText;
            RewardText = rewardText;
            ConditionText = conditionText;
            ConditionMet = conditionMet;
            RewardLines = rewardLines;
        }
    }
}
