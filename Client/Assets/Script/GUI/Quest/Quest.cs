using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Presentation.Quest;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.GUI.Quest
{
    /// <summary>
    /// 퀘스트 창 View(MVI). QuestModel 만 주입받아 State 구독·렌더 + Intent 발행(System/proto 비노출).
    /// 마스터-디테일: 좌측 목록(QuestSlot 동적 풀) 선택 → 우측 정보/조건/보상 + 수주/보상받기 버튼.
    /// 목록 슬롯은 prefab 의 첫 슬롯을 템플릿으로 부족분만 복제(Shop.EnsureItemRows 와 동일 풀 패턴). 열린 동안 캐릭터 이동 차단.
    /// </summary>
    public class Quest : MonoBehaviour
    {
        [SerializeField] private Button btn_close;

        [Header("Quest List")]
        [SerializeField] private ScrollRect questListScrollRect;
        [SerializeField] private List<QuestSlot> questSlots;

        [Header("Quest Info")]
        [SerializeField] private TextMeshProUGUI questName;
        [SerializeField] private TextMeshProUGUI questDescription;

        [Header("Quest Condition")]
        [SerializeField] private GameObject questConditionTitle;
        [SerializeField] private VerticalLayoutGroup questConditionContainerLayout;
        [SerializeField] private List<QuestConditionSlot> questConditionSlots;

        [Header("Quest Reward")]
        [SerializeField] private GameObject questRewardTitle;
        [SerializeField] private GridLayoutGroup questRewardContainerLayout;
        [SerializeField] private List<QuestRewardSlot> questRewardSlots;

        [Header("Quest Status")]
        [SerializeField] private Button btn_Accept; // 수락
        [SerializeField] private Button btn_Decline; // 거절(서버 포기 미지원 — 숨김)
        [SerializeField] private Button btn_Complete; // 완료(보상 받기)

        [Inject] private QuestModel _model;

        private IDisposable _stateSub;
        private IDisposable _toastSub;
        private QuestState _latestState = QuestState.Initial;
        private string _selectedQuestId;
        private bool _wired;

        // 목록 슬롯 동적 풀: prefab 에 배치된 첫 슬롯을 템플릿으로 보관해 부족분만 복제한다.
        private QuestSlot _slotTemplate;
        private Transform _slotParent;

        private void Start()
        {
            if (_model == null)
            {
                Debug.LogError("[Quest] QuestModel 미주입 — 씬 스코프 등록/주입 경로 확인");
                return;
            }

            WireOnce();
            gameObject.AddComponent<UiInputCaptureBehaviour>().Bind(_model.BeginUiCapture, _model.EndUiCapture);

            _stateSub = _model.State.Subscribe(Render);
            _toastSub = _model.OnToast.Subscribe(ShowToast);
            _model.Accept(QuestIntent.Refresh.Instance);
        }

        private void OnEnable()
        {
            if (_model != null && _wired)
                _model.Accept(QuestIntent.Refresh.Instance);
        }

        private void OnDestroy()
        {
            _stateSub?.Dispose();
            _toastSub?.Dispose();
        }

        private void WireOnce()
        {
            if (_wired) return;
            _wired = true;

            // 첫 슬롯 = 동적 복제용 템플릿. 부모(ScrollRect content)도 그 슬롯에서 직접 얻는다.
            if (questSlots != null && questSlots.Count > 0 && questSlots[0] != null)
            {
                _slotTemplate = questSlots[0];
                _slotParent = _slotTemplate.transform.parent;
            }
            else
            {
                Debug.LogWarning("[Quest] 목록 템플릿 슬롯 없음 — prefab 에 QuestSlot 1개를 배치해야 동적 확장이 가능하다");
            }

            if (btn_close != null) btn_close.onClick.AddListener(() => gameObject.SetActive(false));
            if (btn_Decline != null) btn_Decline.gameObject.SetActive(false); // 포기(abandon) 서버 미지원

            // 수락·보상 버튼 폐지 — 수주/보상 수령은 NPC 대화로만. 창은 저널(목록+진행) 전용.
            // 피드백(수락/완료/보상)은 QuestNotifier→AlertPopup 으로 표시.
            if (btn_Accept != null) btn_Accept.gameObject.SetActive(false);
            if (btn_Complete != null) btn_Complete.gameObject.SetActive(false);
        }

        private void Render(QuestState state)
        {
            _latestState = state;
            var quests = state.Quests;

            // 선택 유지(없거나 사라졌으면 첫 항목).
            if (_selectedQuestId == null || quests.All(q => q.QuestId != _selectedQuestId))
                _selectedQuestId = quests.Count > 0 ? quests[0].QuestId : null;

            // 목록 — 부족분 동적 확장 후 바인딩, 남는 슬롯 숨김.
            if (questSlots != null)
            {
                EnsureQuestSlots(quests.Count);
                for (int i = 0; i < questSlots.Count; i++)
                {
                    var slot = questSlots[i];
                    if (slot == null) continue;
                    if (i < quests.Count)
                    {
                        var q = quests[i];
                        slot.gameObject.SetActive(true);
                        slot.Bind(q.Name, q.QuestId == _selectedQuestId, q.IsClaimed,
                            () => Select(q.QuestId));
                    }
                    else
                    {
                        slot.gameObject.SetActive(false);
                    }
                }
            }

            RenderDetail();
        }

        private void Select(string questId)
        {
            _selectedQuestId = questId;
            RenderDetail();
        }

        /// <summary>목록 풀이 needed 개에 못 미치면 템플릿을 복제해 채운다(Shop.EnsureItemRows 동일 패턴).</summary>
        private void EnsureQuestSlots(int needed)
        {
            if (_slotTemplate == null || _slotParent == null) return;
            while (questSlots.Count < needed)
                questSlots.Add(Instantiate(_slotTemplate, _slotParent));
        }

        private void RenderDetail()
        {
            var sel = _selectedQuestId != null
                ? _latestState.Quests.FirstOrDefault(q => q.QuestId == _selectedQuestId)
                : null;

            bool hasSel = sel != null;
            if (questName != null) questName.text = hasSel ? sel.Name : string.Empty;
            if (questDescription != null) questDescription.text = hasSel ? sel.Description : string.Empty;
            if (questConditionTitle != null) questConditionTitle.SetActive(hasSel);
            if (questRewardTitle != null) questRewardTitle.SetActive(hasSel);

            // 조건(현재 단일 목표) — 첫 슬롯에 표시, 나머지 숨김.
            if (questConditionSlots != null)
                for (int i = 0; i < questConditionSlots.Count; i++)
                {
                    var c = questConditionSlots[i];
                    if (c == null) continue;
                    if (hasSel && i == 0)
                    {
                        c.gameObject.SetActive(true);
                        c.Bind(sel.ConditionText, sel.ConditionMet);
                    }
                    else c.gameObject.SetActive(false);
                }

            // 보상 — 항목별 슬롯, 남는 슬롯 숨김.
            if (questRewardSlots != null)
            {
                var lines = hasSel ? sel.RewardLines : null;
                for (int i = 0; i < questRewardSlots.Count; i++)
                {
                    var r = questRewardSlots[i];
                    if (r == null) continue;
                    if (lines != null && i < lines.Count)
                    {
                        r.gameObject.SetActive(true);
                        r.Bind(lines[i]);
                    }
                    else r.gameObject.SetActive(false);
                }
            }

            // 수락·보상 버튼 폐지(NPC 대화로 일원화) — 여기서 다시 켜지 않는다(WireOnce 에서 영구 숨김).
        }

        private void ShowToast(string message) => Debug.Log($"[Quest] {message}");

        [InspectorButton("Quick Setting")]
        private void QuickSetting()
        {
            btn_close = this.FindChildComponentByName<Button>("btn_close");

            questListScrollRect = this.FindChildComponentByName<ScrollRect>("QuestListScrollRect");
            questSlots = questListScrollRect.content.GetComponentsInChildren<QuestSlot>().ToList();

            questName = this.FindChildComponentByName<TextMeshProUGUI>("QuestName");
            questDescription = this.FindChildComponentByName<TextMeshProUGUI>("QuestDescription");

            questConditionTitle = this.FindChildComponentByName("QuestConditionTitle");
            questConditionContainerLayout = this.FindChildComponentByName<VerticalLayoutGroup>("QuestConditionContainerLayout");
            questConditionSlots = questConditionContainerLayout.GetComponentsInChildren<QuestConditionSlot>().ToList();

            questRewardTitle = this.FindChildComponentByName("QuestRewardTitle");
            questRewardContainerLayout = this.FindChildComponentByName<GridLayoutGroup>("QuestRewardContainerLayout");
            questRewardSlots = questRewardContainerLayout.GetComponentsInChildren<QuestRewardSlot>().ToList();

            btn_Accept = this.FindChildComponentByName<Button>("btn_Accept");
            btn_Decline = this.FindChildComponentByName<Button>("btn_Decline");
            btn_Complete = this.FindChildComponentByName<Button>("btn_Complete");
        }
    }
}
