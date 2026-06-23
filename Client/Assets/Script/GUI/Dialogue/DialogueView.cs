using System;
using System.Collections.Generic;
using Game.Core;
using Game.Presentation.Dialogue;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.GUI.Dialogue
{
    /// <summary>
    /// 대화 창 View(MVI). DialogueModel 만 주입받아 State 구독·렌더 + Intent 발행(System/proto 비노출).
    /// 선택지 버튼은 choiceTemplate(첫 자식)을 복제하는 동적 풀(Quest 목록과 동일 패턴). 본문/화자 표시 + 닫기.
    /// </summary>
    public class DialogueView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI speakerText;
        [SerializeField] private TextMeshProUGUI bodyText;
        [SerializeField] private Transform choiceContainer;
        [SerializeField] private Button choiceTemplate;   // 비활성 템플릿(복제 원본)
        [SerializeField] private Button closeButton;

        [Inject] private DialogueModel _model;

        private readonly List<Button> _choicePool = new();
        private IDisposable _stateSub;
        private bool _wired;
        private int _visibleChoiceCount;

        private void Start()
        {
            if (_model == null)
            {
                Debug.LogError("[DialogueView] DialogueModel 미주입 — 씬 스코프 등록/주입 경로 확인");
                return;
            }

            WireOnce();
            if (choiceTemplate != null) choiceTemplate.gameObject.SetActive(false);

            _stateSub = _model.State.Subscribe(Render);
        }

        private void OnDestroy() => _stateSub?.Dispose();

        // 선형 진행: 선택지 버튼이 없어도(본문 전용 바) 클릭/Submit 으로 다음 노드 진행.
        //   선택지 1개  → 그 선택지 선택(진행)
        //   선택지 0개  → 닫기
        //   선택지 2개+ → 분기(선택지 버튼 필요) — 입력 무시(버튼으로만).
        private void Update()
        {
            if (_visibleChoiceCount > 1) return; // 분기는 버튼으로
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            bool advance = (mouse != null && mouse.leftButton.wasPressedThisFrame)
                           || (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame));
            if (!advance) return;

            if (_visibleChoiceCount == 1) _model.Accept(new DialogueIntent.SelectChoice(_firstChoiceIndex));
            else _model.Accept(DialogueIntent.Close.Instance);
        }

        private int _firstChoiceIndex;

        private void WireOnce()
        {
            if (_wired) return;
            _wired = true;
            if (closeButton != null)
                closeButton.onClick.AddListener(() => _model.Accept(DialogueIntent.Close.Instance));
        }

        private void Render(DialogueState state)
        {
            if (speakerText != null)
            {
                speakerText.text = state.Speaker;
                speakerText.gameObject.SetActive(!string.IsNullOrEmpty(state.Speaker));
            }
            if (bodyText != null) bodyText.text = state.BodyText;

            _visibleChoiceCount = state.Choices.Count;
            _firstChoiceIndex = state.Choices.Count > 0 ? state.Choices[0].Index : 0;

            EnsureChoices(state.Choices.Count);
            for (int i = 0; i < _choicePool.Count; i++)
            {
                var btn = _choicePool[i];
                if (i < state.Choices.Count)
                {
                    var choice = state.Choices[i];
                    btn.gameObject.SetActive(true);
                    var label = btn.GetComponentInChildren<TextMeshProUGUI>(true);
                    if (label != null) label.text = choice.Label;
                    btn.onClick.RemoveAllListeners();
                    int idx = choice.Index; // 원본 선택지 인덱스(노출 필터와 무관하게 안정)
                    btn.onClick.AddListener(() => _model.Accept(new DialogueIntent.SelectChoice(idx)));
                }
                else
                {
                    btn.gameObject.SetActive(false);
                }
            }
        }

        private void EnsureChoices(int needed)
        {
            while (_choicePool.Count < needed && choiceTemplate != null && choiceContainer != null)
                _choicePool.Add(Instantiate(choiceTemplate, choiceContainer));
        }

        [InspectorButton("Quick Setting")]
        private void QuickSetting()
        {
            speakerText = this.FindChildComponentByName<TextMeshProUGUI>("SpeakerText");
            bodyText = this.FindChildComponentByName<TextMeshProUGUI>("BodyText");
            var scroll = this.FindChildComponentByName<ScrollRect>("ChoiceList");
            choiceContainer = scroll != null ? scroll.content : this.FindChildComponentByName("ChoiceContainer")?.transform;
            choiceTemplate = this.FindChildComponentByName<Button>("ChoiceTemplate");
            closeButton = this.FindChildComponentByName<Button>("btn_close");
        }
    }
}
