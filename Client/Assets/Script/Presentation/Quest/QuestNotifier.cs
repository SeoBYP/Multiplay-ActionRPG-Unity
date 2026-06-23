using System;
using System.Collections.Generic;
using Game.System.Quest;
using R3;

namespace Game.Presentation.Quest
{
    /// <summary>알림 종류 — GUI 가 PopupGlowType 으로 매핑(Presentation 은 GUI 타입 비참조).</summary>
    public enum QuestNoticeKind { Accepted, Completed, Claimed }

    /// <summary>한 건의 퀘스트 알림(불변). GUI 가 AlertPopup 으로 표시.</summary>
    public sealed class QuestNotice
    {
        public string Title { get; }
        public string Message { get; }
        public QuestNoticeKind Kind { get; }

        public QuestNotice(string title, string message, QuestNoticeKind kind)
        {
            Title = title;
            Message = message;
            Kind = kind;
        }
    }

    /// <summary>
    /// 퀘스트 알림 단일 소스(수락/완료/보상). QuestModel·DialogueModel 이 GetQuests 직후 <see cref="Sync"/>,
    /// NPC 대화에서 수락/수령 성공 시 <see cref="NotifyAccepted"/>/<see cref="NotifyClaimed"/> 호출
    /// → GUI(QuestNotificationPresenter)가 OnNotice 를 구독해 팝업 표시.
    /// 완료 알림은 상태가 Completed 로 "전이"할 때만 1회(매 조회마다 반복 금지 = 스팸 방지).
    /// 보상받기 버튼 폐지 → 보상 수령은 NPC 대화 경로(DialogueModel)만.
    /// </summary>
    public sealed class QuestNotifier : IDisposable
    {
        private readonly Subject<QuestNotice> _notice = new();
        public Observable<QuestNotice> OnNotice => _notice;

        // 마지막으로 관측한 퀘스트(상태 전이 판정 + 이름/보상 조회용).
        private readonly Dictionary<string, QuestData> _seen = new();

        /// <summary>GetQuests 결과 반영. 새로 Completed 로 전이한 퀘스트마다 완료 알림 1회.</summary>
        public void Sync(IReadOnlyList<QuestData> quests)
        {
            if (quests == null) return;
            foreach (var q in quests)
            {
                if (q == null) continue;
                var prev = _seen.GetValueOrDefault(q.QuestId);
                bool becameCompleted = q.Status == QuestProgressState.Completed
                                       && (prev == null || prev.Status != QuestProgressState.Completed);
                _seen[q.QuestId] = q;
                if (becameCompleted)
                    _notice.OnNext(new QuestNotice("퀘스트 완료", $"{q.Name}\nNPC에게 보상을 받으세요.", QuestNoticeKind.Completed));
            }
        }

        /// <summary>대화에서 수주 성공 시.</summary>
        public void NotifyAccepted(string questId)
            => _notice.OnNext(new QuestNotice("퀘스트 수락", NameOf(questId), QuestNoticeKind.Accepted));

        /// <summary>대화에서 보상 수령 성공 시(보상 요약 포함).</summary>
        public void NotifyClaimed(string questId)
        {
            var q = _seen.GetValueOrDefault(questId);
            var reward = q != null ? RewardSummary(q.Reward) : string.Empty;
            var msg = string.IsNullOrEmpty(reward) ? NameOf(questId) : $"{NameOf(questId)}\n보상: {reward}";
            _notice.OnNext(new QuestNotice("보상 획득", msg, QuestNoticeKind.Claimed));
        }

        private string NameOf(string questId)
            => _seen.TryGetValue(questId, out var q) && q != null ? q.Name : questId;

        private static string RewardSummary(QuestRewardData r)
        {
            var parts = new List<string>(3);
            if (r.Exp > 0) parts.Add($"경험치 {r.Exp}");
            if (r.Gold > 0) parts.Add($"골드 {r.Gold}");
            if (!string.IsNullOrEmpty(r.ItemId) && r.ItemQty > 0) parts.Add($"{r.ItemId} x{r.ItemQty}");
            return string.Join(", ", parts);
        }

        public void Dispose() => _notice.Dispose();
    }
}
