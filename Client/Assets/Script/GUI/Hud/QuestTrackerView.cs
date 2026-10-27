using System;
using System.Collections.Generic;
using Game.Presentation.Quest;
using R3;
using TMPro;
using UnityEngine;
using VContainer;

namespace Game.GUI.Hud
{
    /// <summary>
    /// HUD 퀘스트 추적기 — 진행 중(Accepted/Completed) 퀘스트의 이름+조건 표시.
    /// QuestModel 은 Main 전용 스코프 등록이라 하드 [Inject] 하면 던전 GameHud 가 깨진다
    /// → IObjectResolver.TryResolve 로 선택 주입(없으면 추적기 숨김).
    /// </summary>
    public sealed class QuestTrackerView : MonoBehaviour
    {
        [SerializeField] private GameObject root;             // 추적기 루트(진행중 0개면 숨김)
        [SerializeField] private Transform rowContainer;      // 행 부모(VerticalLayoutGroup)
        [SerializeField] private TextMeshProUGUI rowTemplate; // 행 템플릿(비활성 — 복제 원본)

        [Inject] private IObjectResolver _resolver;

        private QuestModel _model;
        private IDisposable _sub;
        private IDisposable _noticeSub;
        private readonly List<TextMeshProUGUI> _rows = new();

        private void Start()
        {
            if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);

            // QuestModel 이 스코프에 있으면(Main) 구독, 없으면(던전 등) 추적기 숨김 — DI 깨짐 방지.
            if (_resolver != null && _resolver.TryResolve(typeof(QuestModel), out var resolved) && resolved is QuestModel model)
            {
                _model = model;
                _sub = _model.State.Subscribe(Render);

                // NPC 대화(DialogueModel)가 IQuestService 를 직접 호출하면 QuestModel.State 가 갱신되지 않는다
                // → 두 경로의 단일 알림 소스 QuestNotifier 를 구독해 재조회한다.
                if (_resolver.TryResolve(typeof(QuestNotifier), out var n) && n is QuestNotifier notifier)
                    _noticeSub = notifier.OnNotice.Subscribe(_ => _model.Accept(QuestIntent.Refresh.Instance));

                _model.Accept(QuestIntent.Refresh.Instance);
            }
            else if (root != null)
            {
                root.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            _sub?.Dispose();
            _noticeSub?.Dispose();
        }

        /// <summary>
        /// 진행 중(Accepted/Completed) 퀘스트만 추린다 — 테스트용 순수 함수.
        /// GUI 는 QuestProgressState 를 몰라 bool 로 판정: 미수주(CanAccept)·수령완료(IsClaimed) 제외 = 진행중.
        /// </summary>
        public static List<QuestEntryModel> InProgress(IReadOnlyList<QuestEntryModel> quests)
        {
            var list = new List<QuestEntryModel>();
            if (quests == null) return list;
            foreach (var q in quests)
                if (q != null && !q.CanAccept && !q.IsClaimed)
                    list.Add(q);
            return list;
        }

        private void Render(QuestState state)
        {
            var active = InProgress(state.Quests);
            if (root != null) root.SetActive(active.Count > 0);

            EnsureRows(active.Count);
            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                if (row == null) continue;
                if (i < active.Count)
                {
                    var q = active[i];
                    row.gameObject.SetActive(true);
                    row.text = q.CanClaim   // CanClaim = Completed(보상받기 가능)
                        ? $"{q.Name}  (완료)"
                        : $"{q.Name}  {q.ConditionText}";
                }
                else
                {
                    row.gameObject.SetActive(false);
                }
            }
        }

        private void EnsureRows(int needed)
        {
            if (rowTemplate == null || rowContainer == null) return;
            while (_rows.Count < needed)
                _rows.Add(Instantiate(rowTemplate, rowContainer));
        }
    }
}
