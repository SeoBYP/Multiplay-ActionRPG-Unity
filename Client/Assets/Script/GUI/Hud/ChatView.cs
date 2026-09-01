using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Gameplay.Input;
using Game.Presentation.Chat;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;

namespace Game.GUI.OutGame
{
    /// <summary>
    /// HUD 채팅 View. **로그와 입력줄 모두 항상 보인다.**
    ///
    /// 입력줄을 Enter 로만 여는 방식이었다가 바꿨다 — 그러면 Enter 라우팅이 한 번이라도 막히는 순간
    /// **입력 수단 자체가 화면에 없어** "채팅이 안 된다"가 된다. 항상 띄워 두면 클릭해서도 칠 수 있다.
    ///
    /// MVI 규칙: <see cref="ChatModel"/> 하나만 주입받는다(서버·proto 타입을 모른다).
    ///
    /// 입력 흐름 — 같은 Enter 가 상태에 따라 다른 입력 맵에서 처리된다:
    ///   비입력: Player 맵 → InputRouter → TryHandle(Chat) → 입력줄 포커스 + UI 점유(Player 맵 OFF)
    ///   입력중: UI 맵 → InputField.onSubmit → 전송 → 포커스 해제 + 점유 해제
    /// 클릭으로 직접 포커스한 경우도 <c>onSelect</c> 가 같은 점유를 건다(안 그러면 WASD 가 캐릭터를 움직인다).
    ///
    /// 채널 드롭다운은 **상황에 따라 항목이 바뀐다** — Main(방 미소속) = 전체·개인 / 방·던전 = 방·개인.
    /// 서버가 "방에 속했으면 Room, 아니면 Global" 로 정하므로, 고를 수 없는 항목은 아예 보여주지 않는다
    /// (고르게 해 놓고 서버가 무시하면 그게 거짓말이다).
    /// </summary>
    public sealed class ChatView : MonoBehaviour, IInputHandler
    {
        private const string GlobalOption  = "전체";
        private const string RoomOption    = "방";
        private const string WhisperOption = "개인";

        [Inject] private ChatModel _model;
        [Inject] private IInputRouter _inputRouter;

        [Header("Chat")]
        [Tooltip("말풍선이 쌓이는 컨테이너(LogScroll/Viewport/Content).")]
        [SerializeField] private RectTransform logContent;
        [Tooltip("한 줄 템플릿. 이 오브젝트를 복제해 쓰고 원본은 숨긴다(별도 프리팹 에셋 불필요).")]
        [SerializeField] private ChatBubbleView bubbleTemplate;
        [Tooltip("로그 스크롤. 새 줄이 오면 항상 맨 아래로 내린다. 없어도 동작은 무해.")]
        [SerializeField] private ScrollRect logScroll;
        [Tooltip("입력줄. 항상 보이며 Enter 또는 클릭으로 포커스한다. 포커스 동안 게임플레이 입력은 잠긴다.")]
        [SerializeField] private TMP_InputField inputField;
        [Tooltip("채널 드롭다운. 항목은 방 소속 여부에 따라 런타임에 다시 채워진다.")]
        [SerializeField] private TMP_Dropdown channelDropdown;

        private readonly List<ChatBubbleView> _bubbles = new List<ChatBubbleView>();
        private bool _typing;
        private bool _lastInRoom;
        private bool _optionsBuilt;

        // UI 우선순위 100 (GameHud·LobbyViewController 와 동일) — 월드 인터랙션보다 먼저 소비한다.
        public int Priority => 100;

        /// <summary>드롭다운에서 '개인'(귓속말)이 선택돼 있는가. 항목은 항상 [일반, 개인] 두 개다.</summary>
        private bool WhisperSelected => channelDropdown != null && channelDropdown.value == 1;

        private void Start()
        {
            // 라우터 등록은 Start 에서 — [Inject] 필드 주입이 OnEnable 보다 늦을 수 있다(unity-client.md).
            _inputRouter?.Register(this);

            if (bubbleTemplate != null)
                bubbleTemplate.Hide(); // 원본은 항상 숨김 — 복제본만 보인다

            if (inputField != null)
            {
                inputField.onSubmit.AddListener(OnSubmit);
                inputField.onSelect.AddListener(_ => BeginCapture());   // 클릭으로 포커스한 경우도 점유
                inputField.onDeselect.AddListener(_ => ReleaseCapture());
                inputField.characterLimit = ChatModel.MaxMessageLength;
                inputField.gameObject.SetActive(true);
            }

            if (_model == null)
                return;

            RebuildChannelOptions();
            Render(); // 씬을 옮겨 HUD 가 새로 생겨도 Model 이 들고 있던 지난 로그가 그대로 보인다.

            _model.OnLine
                .Subscribe(_ => Render())
                .AddTo(destroyCancellationToken);
        }

        private void Update()
        {
            // 방 입장/던전 입장/퇴장은 채팅과 다른 경로로 일어난다 — 값이 바뀌었을 때만 항목을 다시 만든다.
            if (_model != null && _optionsBuilt && _model.IsInRoom != _lastInRoom)
                RebuildChannelOptions();
        }

        private void OnDestroy()
        {
            _inputRouter?.Unregister(this);
            ReleaseCapture(); // 타이핑 중 씬이 바뀌면 점유가 남아 이동이 영영 막힌다
        }

        private void OnDisable() => ReleaseCapture();

        // ── IInputHandler ─────────────────────────

        public bool TryHandle(GameInputAction action)
        {
            if (action != GameInputAction.Chat || _model == null || inputField == null)
                return false;

            if (_typing)
                return true; // 입력 중 Enter 는 UI 맵(InputField)이 처리한다 — 여기까지 오지 않지만 방어

            StartTyping();
            return true;
        }

        // ── 채널 드롭다운 ─────────────────────────

        /// <summary>
        /// 방 소속 여부에 따라 항목을 다시 채운다. 첫 항목은 일반 채널(전체 또는 방), 둘째는 개인(귓속말).
        /// 선택 인덱스는 유지한다 — 귓속말을 고른 채 던전에 들어가도 계속 귓속말이다.
        /// </summary>
        private void RebuildChannelOptions()
        {
            if (channelDropdown == null) return;

            _lastInRoom = _model != null && _model.IsInRoom;
            _optionsBuilt = true;

            int keep = channelDropdown.value;
            channelDropdown.ClearOptions();
            channelDropdown.AddOptions(new List<string>
            {
                _lastInRoom ? RoomOption : GlobalOption,
                WhisperOption,
            });
            channelDropdown.SetValueWithoutNotify(Mathf.Clamp(keep, 0, 1));
            channelDropdown.RefreshShownValue();

            UpdatePlaceholder();
        }

        private void UpdatePlaceholder()
        {
            if (inputField == null || inputField.placeholder is not TextMeshProUGUI placeholder) return;

            placeholder.text = WhisperSelected
                ? "닉네임 내용  (첫 단어가 받는 사람)"
                : "Enter 입력  ·  귓속말은 개인 선택";
        }

        // ── 입력줄 ───────────────────────────────

        private void StartTyping()
        {
            BeginCapture(); // Player 맵 OFF — 타이핑이 이동·공격으로 새지 않게
            FocusNextFrameAsync().Forget();
        }

        /// <summary>입력 점유 획득(중복 호출 무해 — Enter 경로와 클릭 경로가 겹칠 수 있다).</summary>
        private void BeginCapture()
        {
            if (_typing) return;
            _typing = true;
            _model?.BeginUiCapture();
        }

        /// <summary>입력 점유 해제. 포커스가 풀리는 모든 경로(전송·클릭 이탈·비활성)가 여기로 모인다.</summary>
        private void ReleaseCapture()
        {
            if (!_typing) return;
            _typing = false;
            _model?.EndUiCapture();
        }

        /// <summary>
        /// 활성화와 같은 프레임에 포커스를 주면 방금 누른 Enter 가 그대로 '전송'으로 먹혀 빈 줄이 나간다.
        /// 한 프레임 미뤄 그 입력을 흘려보낸다.
        /// </summary>
        private async UniTaskVoid FocusNextFrameAsync()
        {
            await UniTask.NextFrame(destroyCancellationToken).SuppressCancellationThrow();
            if (!_typing || inputField == null) return;

            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(inputField.gameObject);
            inputField.ActivateInputField();
        }

        private void OnSubmit(string text)
        {
            if (!_typing) return;

            if (string.IsNullOrWhiteSpace(text) || _model.Send(text, WhisperSelected))
            {
                StopTyping(); // 빈 줄로 Enter = 그냥 닫기
                return;
            }

            // 보낼 수 없는 입력(대상만 쓴 귓속말, 길이 초과) — 입력을 지우지 않고 고칠 기회를 준다.
            inputField.ActivateInputField();
        }

        /// <summary>전송·취소 후 정리 — 입력줄은 숨기지 않고 포커스만 푼다.</summary>
        private void StopTyping()
        {
            if (inputField != null)
            {
                inputField.text = string.Empty;
                inputField.DeactivateInputField();
                if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == inputField.gameObject)
                    EventSystem.current.SetSelectedGameObject(null);
            }

            ReleaseCapture();
        }

        // ── 로그 ────────────────────────────────

        /// <summary>
        /// Model 의 최근 줄(최대 100)을 말풍선 행에 바인딩한다. 행은 풀로 재사용하고 남는 행은 숨긴다
        /// — 상한이 Model 한 곳에만 있으므로 여기서 개수를 따로 관리하지 않는다.
        /// </summary>
        private void Render()
        {
            if (logContent == null || bubbleTemplate == null || _model == null) return;

            var lines = _model.Recent;

            while (_bubbles.Count < lines.Count)
            {
                var bubble = Instantiate(bubbleTemplate, logContent);
                bubble.name = $"ChatBubble_{_bubbles.Count}";
                _bubbles.Add(bubble);
            }

            for (int i = 0; i < _bubbles.Count; i++)
            {
                if (i < lines.Count) _bubbles[i].Bind(lines[i]);
                else                 _bubbles[i].Hide();
            }

            ScrollToBottom();
        }

        private void ScrollToBottom()
        {
            if (logScroll == null) return;
            Canvas.ForceUpdateCanvases();
            logScroll.verticalNormalizedPosition = 0f;
        }
    }
}
