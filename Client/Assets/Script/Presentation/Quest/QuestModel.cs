using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.System.Input;
using Game.System.Progression;
using Game.System.Quest;
using R3;

namespace Game.Presentation.Quest
{
    /// <summary>
    /// 퀘스트 화면 MVI Model. View는 Accept(Intent)만, Model은 State만 발행(ShopModel 동형).
    /// 수주/보상 = 서버 권위(IQuestService) → 성공 시 목록 재조회 + 토스트. GUI는 이 Model만 주입받는다.
    /// </summary>
    public sealed class QuestModel : IDisposable
    {
        private readonly IQuestService _service;
        private readonly IInputContext _inputContext;
        private readonly QuestNotifier _notifier;
        private readonly PlayerProgressionHolder _progression;
        private readonly CancellationTokenSource _cts = new();

        private readonly ReactiveProperty<QuestState> _state = new(QuestState.Initial);
        public ReadOnlyReactiveProperty<QuestState> State => _state.ToReadOnlyReactiveProperty();

        private readonly Subject<string> _onToast = new();
        /// <summary>일회성 토스트(수주/수령 결과) — View 표시 후 종료.</summary>
        public Observable<string> OnToast => _onToast;

        public QuestModel(IQuestService service, IInputContext inputContext = null, QuestNotifier notifier = null,
            PlayerProgressionHolder progression = null)
        {
            _service = service;
            _inputContext = inputContext;
            _notifier = notifier;

            // 킬 진행도 실시간 반영: 킬→서버 진행 갱신(ReportKill 서버내부) 직후 MainMonsterSpawner 가 진행/스탯 RefreshAsync
            // → holder.OnChanged 발화. 이를 구독해 퀘스트 목록을 재조회 → 진행 카운트(2/3→3/3)·완료 전이 즉시 반영.
            _progression = progression;
            if (_progression != null)
                _progression.OnChanged += OnProgressionChanged;
        }

        private void OnProgressionChanged() => RefreshAsync().Forget();

        public void BeginUiCapture() => _inputContext?.EnterUi();
        public void EndUiCapture() => _inputContext?.ExitUi();

        public void Accept(QuestIntent intent)
        {
            switch (intent)
            {
                case QuestIntent.Refresh:
                    RefreshAsync().Forget();
                    break;
                case QuestIntent.Accept accept:
                    AcceptAsync(accept.QuestId).Forget();
                    break;
                case QuestIntent.Claim claim:
                    ClaimAsync(claim.QuestId).Forget();
                    break;
            }
        }

        private async UniTaskVoid AcceptAsync(string questId)
        {
            try
            {
                var result = await _service.AcceptAsync(questId, _cts.Token);
                _onToast.OnNext(result == QuestResult.Success ? "퀘스트를 수주했습니다" : "수주할 수 없습니다");
                if (result == QuestResult.Success)
                    RefreshAsync().Forget();
            }
            catch (OperationCanceledException) { }
        }

        private async UniTaskVoid ClaimAsync(string questId)
        {
            try
            {
                var result = await _service.ClaimRewardAsync(questId, _cts.Token);
                _onToast.OnNext(result == QuestResult.Success ? "보상을 받았습니다" : "보상을 받을 수 없습니다");
                if (result == QuestResult.Success)
                    RefreshAsync().Forget();
            }
            catch (OperationCanceledException) { }
        }

        private async UniTaskVoid RefreshAsync()
        {
            _state.Value = _state.Value.With(isLoading: true, clearError: true);
            try
            {
                var (result, quests) = await _service.GetQuestsAsync(_cts.Token);
                if (result != QuestResult.Success)
                {
                    _state.Value = _state.Value.With(isLoading: false, error: result.ToString());
                    return;
                }

                _notifier?.Sync(quests);   // 저널 열 때 완료 전이 감지 → 완료 알림

                var models = new List<QuestEntryModel>(quests.Count);
                foreach (var q in quests)
                    models.Add(new QuestEntryModel(
                        q.QuestId, q.Name, q.Description, q.RequiredCount, q.CurrentProgress, q.Status,
                        ProgressText(q.Status, q.CurrentProgress, q.RequiredCount),
                        RewardText(q.Reward),
                        ConditionText(q.Objective, q.TargetId, q.CurrentProgress, q.RequiredCount),
                        q.CurrentProgress >= q.RequiredCount,
                        RewardLines(q.Reward)));

                _state.Value = _state.Value.With(quests: models, isLoading: false, clearError: true);
            }
            catch (OperationCanceledException) { }
        }

        private static string ConditionText(QuestObjectiveKind objective, string targetId, int cur, int req)
        {
            var verb = objective switch
            {
                QuestObjectiveKind.CollectItem => "수집",
                QuestObjectiveKind.TalkToNpc => "대화",
                _ => "처치",
            };
            return $"{targetId} {verb} {cur}/{req}";
        }

        private static string ProgressText(QuestProgressState status, int cur, int req) => status switch
        {
            QuestProgressState.NotAccepted => "미수주",
            QuestProgressState.Completed => "완료",
            QuestProgressState.Claimed => "수령 완료",
            _ => $"{cur} / {req}",
        };

        private static string RewardText(QuestRewardData r)
        {
            var sb = new StringBuilder("보상:");
            if (r.Exp > 0) sb.Append($" 경험치 {r.Exp}");
            if (r.Gold > 0) sb.Append($" 골드 {r.Gold}");
            if (!string.IsNullOrEmpty(r.ItemId) && r.ItemQty > 0) sb.Append($" {r.ItemId} x{r.ItemQty}");
            return sb.ToString();
        }

        /// <summary>보상을 항목별 문자열 리스트로(reward 슬롯 분리 표시용).</summary>
        private static List<string> RewardLines(QuestRewardData r)
        {
            var lines = new List<string>(3);
            if (r.Exp > 0) lines.Add($"경험치 {r.Exp}");
            if (r.Gold > 0) lines.Add($"골드 {r.Gold}");
            if (!string.IsNullOrEmpty(r.ItemId) && r.ItemQty > 0) lines.Add($"{r.ItemId} x{r.ItemQty}");
            return lines;
        }

        public void Dispose()
        {
            if (_progression != null)
                _progression.OnChanged -= OnProgressionChanged;
            _cts.Cancel();
            _cts.Dispose();
            _state.Dispose();
            _onToast.Dispose();
        }
    }
}
