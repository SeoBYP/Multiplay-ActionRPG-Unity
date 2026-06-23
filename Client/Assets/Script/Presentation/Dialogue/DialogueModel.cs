using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.System.Dialogue;
using Game.System.Input;
using Game.System.Quest;
using R3;
using UnityEngine;

namespace Game.Presentation.Dialogue
{
    /// <summary>
    /// 대화 화면 MVI Model. View는 Accept(Intent)만, Model은 State만 발행(ShopModel 동형).
    /// 노드 순회(GoTo)·종료(EndDialogue) + 카메라 구도(A3) + **퀘스트 연동(Phase B)**:
    ///   선택지 action AcceptQuest/ClaimQuest → IQuestService(서버 권위) 재사용,
    ///   선택지 showIf(QuestStatus) → 퀘스트 상태로 노출 조건부(수락/보상받기 선택지 분기).
    /// 퀘스트 상태는 대화 진입·수락/수령 직후 GetQuests 로 캐시(노드 렌더는 캐시로 동기 평가).
    /// GUI(DialogueView)는 이 Model만 주입받는다. IDialogueLauncher 구현(NPC→Open).
    /// </summary>
    public sealed class DialogueModel : IDialogueLauncher, IDisposable
    {
        private readonly DialogueCatalog _catalog;
        private readonly IInputContext _inputContext;
        private readonly IDialogueCamera _camera;
        private readonly IQuestService _quest;
        private readonly Game.Presentation.Quest.QuestNotifier _notifier;

        private readonly ReactiveProperty<DialogueState> _state = new(DialogueState.Closed);
        public ReadOnlyReactiveProperty<DialogueState> State => _state.ToReadOnlyReactiveProperty();

        private readonly Dictionary<string, QuestProgressState> _questStates = new();

        private DialogueDefinition _current;
        private DialogueNode _node;
        private CancellationTokenSource _cts;

        public DialogueModel(DialogueCatalog catalog = null, IInputContext inputContext = null,
            IDialogueCamera camera = null, IQuestService quest = null,
            Game.Presentation.Quest.QuestNotifier notifier = null)
        {
            _catalog = catalog;
            _inputContext = inputContext;
            _camera = camera;
            _quest = quest;
            _notifier = notifier;
        }

        /// <summary>IDialogueLauncher — NPC(Gameplay)가 호출하는 진입점.</summary>
        public void Open(string npcId) => Start(npcId);

        public void Start(string npcId)
        {
            var def = _catalog != null ? _catalog.Get(npcId) : null;
            if (def == null)
            {
                Debug.LogWarning($"[DialogueModel] npcId '{npcId}' 대화 없음(DialogueCatalog 확인)");
                return;
            }

            _current = def;
            _inputContext?.EnterUi();
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            StartAsync(def, npcId, _cts.Token).Forget();
        }

        private async UniTaskVoid StartAsync(DialogueDefinition def, string npcId, CancellationToken ct)
        {
            // TalkToNpc 진행 보고(Phase C) — 대화 시작 = NPC 와 대화. 서버 권위·멱등(잘못된 npcId/미수주는 무진행).
            if (_quest != null) await _quest.ReportTalkAsync(npcId, ct);
            if (ct.IsCancellationRequested) return;

            await RefreshQuestStatesAsync(ct);          // 진입 시 퀘스트 상태 캐시(ReportTalk 반영 후 — 조건부 선택지 평가용)
            if (ct.IsCancellationRequested) return;
            RenderNode(def.GetStartNode());
        }

        public void Accept(DialogueIntent intent)
        {
            switch (intent)
            {
                case DialogueIntent.SelectChoice select:
                    SelectChoice(select.Index);
                    break;
                case DialogueIntent.Close:
                    Close();
                    break;
            }
        }

        private void SelectChoice(int choiceIndex)
        {
            if (_node?.choices == null || choiceIndex < 0 || choiceIndex >= _node.choices.Count)
                return;

            var choice = _node.choices[choiceIndex];
            switch (choice.action)
            {
                case DialogueActionKind.GoTo:
                    Navigate(choice.targetNodeId);
                    break;

                case DialogueActionKind.AcceptQuest:
                    QuestActionAsync(choice, accept: true, _cts.Token).Forget();
                    break;
                case DialogueActionKind.ClaimQuest:
                    QuestActionAsync(choice, accept: false, _cts.Token).Forget();
                    break;

                case DialogueActionKind.EndDialogue:
                default:
                    Close();
                    break;
            }
        }

        /// <summary>퀘스트 수락/보상수령(서버 권위) → 상태 갱신 → 대상 노드 이동(없으면 현재 노드 재평가).</summary>
        private async UniTaskVoid QuestActionAsync(DialogueChoice choice, bool accept, CancellationToken ct)
        {
            if (_quest != null && !string.IsNullOrEmpty(choice.questId))
            {
                var result = accept
                    ? await _quest.AcceptAsync(choice.questId, ct)
                    : await _quest.ClaimRewardAsync(choice.questId, ct);
                if (ct.IsCancellationRequested) return;
                await RefreshQuestStatesAsync(ct);      // 상태 변경 반영(완료→Claimed 등). Notifier.Sync 도 여기서.
                if (ct.IsCancellationRequested) return;

                if (result == QuestResult.Success)      // 성공 시에만 팝업 알림(이름·보상은 Sync 캐시에서)
                {
                    if (accept) _notifier?.NotifyAccepted(choice.questId);
                    else _notifier?.NotifyClaimed(choice.questId);
                }
            }

            if (!string.IsNullOrEmpty(choice.targetNodeId)) Navigate(choice.targetNodeId);
            else if (_node != null) RenderNode(_node);   // 현재 노드 재렌더(조건부 선택지 갱신)
            else Close();
        }

        private void Navigate(string targetNodeId)
        {
            var next = _current != null ? _current.GetNode(targetNodeId) : null;
            if (next != null) RenderNode(next);
            else Close(); // 끊긴 엣지 = 종료(저작 실수 방어)
        }

        private void RenderNode(DialogueNode node)
        {
            _node = node;
            if (node == null) { Close(); return; }

            _camera?.SetShot(node.shot);

            var visible = new List<DialogueChoiceView>(node.choices.Count);
            for (int i = 0; i < node.choices.Count; i++)
            {
                if (IsChoiceVisible(node.choices[i]))
                    visible.Add(new DialogueChoiceView(node.choices[i].label, i));
            }

            _state.Value = new DialogueState(true, node.speaker ?? string.Empty, node.bodyText ?? string.Empty, visible,
                _current != null ? _current.hideObjects : null);
        }

        /// <summary>showIf 평가 — 캐시된 퀘스트 상태 기준. 미등록 퀘스트는 NotAccepted 취급.</summary>
        private bool IsChoiceVisible(DialogueChoice c)
        {
            if (c.showIf == DialogueShowCondition.Always) return true;
            if (string.IsNullOrEmpty(c.conditionQuestId)) return true; // 조건 대상 미지정(저작 실수) → 숨기지 않음

            var st = _questStates.GetValueOrDefault(c.conditionQuestId, QuestProgressState.NotAccepted);
            return c.showIf switch
            {
                DialogueShowCondition.QuestNotAccepted => st == QuestProgressState.NotAccepted,
                DialogueShowCondition.QuestInProgress => st == QuestProgressState.Accepted,
                DialogueShowCondition.QuestCompleted => st == QuestProgressState.Completed,
                DialogueShowCondition.QuestClaimed => st == QuestProgressState.Claimed,
                _ => true,
            };
        }

        private async UniTask RefreshQuestStatesAsync(CancellationToken ct)
        {
            _questStates.Clear();
            if (_quest == null) return;

            var (result, quests) = await _quest.GetQuestsAsync(ct);
            if (result != QuestResult.Success || quests == null) return;
            foreach (var q in quests)
                _questStates[q.QuestId] = q.Status;
            _notifier?.Sync(quests);   // 완료 전이 감지 → 완료 알림(이름/보상 캐시 갱신)
        }

        private void Close()
        {
            _node = null;
            _current = null;
            _cts?.Cancel();
            _inputContext?.ExitUi();
            _camera?.Exit();
            _state.Value = DialogueState.Closed;
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _state.Dispose();
        }
    }
}
