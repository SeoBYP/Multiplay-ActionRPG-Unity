using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Game.Gameplay.Input;
using Game.GUI.OutGame;
using Game.Presentation.Chat;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode.InGame
{
    /// <summary>
    /// **실제 키 입력**으로 채팅 입력줄을 여닫는 전 경로.
    ///
    /// EditMode `ChatInputRoutingTests` 는 라우팅(Enter→GameInputAction.Chat)까지만 봤다.
    /// 여기서는 진짜 <see cref="ChatView"/> 가 그 신호를 받아 입력줄을 열고,
    /// **입력 점유(Player 맵 OFF)** 까지 거는지 — 그리고 닫을 때 되돌리는지를 본다.
    /// (그 사이 WASD·공격이 살아 있으면 채팅을 치는 동안 캐릭터가 움직인다.)
    /// </summary>
    [TestFixture]
    public class ChatInputFlowTests : InputTestFixture
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        private Keyboard _keyboard;
        private PlayerInputActions _actions;
        private InputRouter _router;
        private InputContext _inputContext;
        private ControllableChatGrpcService _grpc;
        private ChatModel _model;
        private ChatView _view;
        private TMP_InputField _inputField;
        private EventSystem _eventSystem;
        private readonly List<GameObject> _objects = new List<GameObject>();

        public override void Setup()
        {
            base.Setup();

            _keyboard = InputSystem.AddDevice<Keyboard>();
            _actions  = new PlayerInputActions();
            _router   = new InputRouter(_actions);
            _router.Initialize();
            _actions.Player.Enable(); // 평소엔 전역(GlobalInputInitializer)이 켠다

            _inputContext = new InputContext(_actions);
            _grpc  = new ControllableChatGrpcService();
            _model = HudChatTestDouble.CreateConnectedModel(_grpc, _inputContext);

            BuildScene();
        }

        public override void TearDown()
        {
            _router.Dispose();
            _model.Dispose();

            foreach (var obj in _objects)
                if (obj != null) Object.DestroyImmediate(obj);
            _objects.Clear();

            // Disable 없이 asset 만 파괴하면 PlayerInputActions 종료자가 맵 누수 assert 를 띄운다.
            // 그 로그는 GC 시점에 붙어 "뒤이어 실행되는 무관한 테스트"를 실패시킨다.
            _actions.Disable();
            if (_actions.asset != null)
                Object.DestroyImmediate(_actions.asset);

            base.TearDown();
        }

        [UnityTest]
        public IEnumerator Enter를_누르면_입력줄이_열리고_게임플레이_입력이_잠긴다()
        {
            yield return null; // ChatView.Start → 라우터 등록

            Assert.IsTrue(_inputField.gameObject.activeSelf, "입력줄은 항상 보여야 한다(클릭으로도 칠 수 있게).");
            Assert.IsTrue(_actions.Player.enabled, "포커스 전에는 게임플레이 입력이 살아 있어야 한다.");

            Press(_keyboard.enterKey);
            Release(_keyboard.enterKey);
            // 포커스는 한 프레임 뒤(같은 프레임에 주면 그 Enter 가 전송으로 먹힌다) — 여유를 두고 확인
            yield return null;
            yield return null;
            yield return null;

            Assert.AreEqual(_inputField.gameObject, _eventSystem.currentSelectedGameObject,
                "Enter 를 눌렀는데 입력줄이 포커스되지 않았다.");
            Assert.IsFalse(_actions.Player.enabled, "입력 중에는 Player 맵이 꺼져야 한다(타이핑이 이동으로 샌다).");
        }

        [UnityTest]
        public IEnumerator 입력줄을_닫으면_게임플레이_입력이_돌아온다()
        {
            yield return null;

            Press(_keyboard.enterKey);
            Release(_keyboard.enterKey);
            yield return null;

            _inputField.text = "안녕하세요";
            _inputField.onSubmit.Invoke(_inputField.text); // UI 맵이 하는 일(전송 Enter)

            Assert.IsEmpty(_inputField.text, "전송 후 입력줄이 비워져야 한다.");
            Assert.IsTrue(_actions.Player.enabled, "전송 후 게임플레이 입력이 복구돼야 한다.");
            Assert.AreEqual("안녕하세요", _grpc.LastSentMessage(), "입력한 문장이 서버로 가지 않았다.");
        }

        [UnityTest]
        public IEnumerator 입력_중_Enter가_다시_열기로_라우팅되지_않는다()
        {
            yield return null;

            Press(_keyboard.enterKey);
            Release(_keyboard.enterKey);
            yield return null;

            // 입력 중에는 Player 맵이 꺼져 있으므로 이 Enter 는 라우터까지 오지 않는다.
            Press(_keyboard.enterKey);
            Release(_keyboard.enterKey);
            yield return null;

            Assert.IsFalse(_actions.Player.enabled, "여전히 입력 점유 중이어야 한다.");
            Assert.IsEmpty(_grpc.Sent, "열기 키가 빈 줄을 보내면 안 된다.");
        }

        [UnityTest]
        public IEnumerator 빈_줄로_전송하면_보내지_않고_닫기만_한다()
        {
            yield return null;

            Press(_keyboard.enterKey);
            Release(_keyboard.enterKey);
            yield return null;

            _inputField.onSubmit.Invoke(string.Empty);

            Assert.IsTrue(_actions.Player.enabled, "빈 줄로 닫아도 게임플레이 입력은 복구돼야 한다.");
            Assert.IsEmpty(_grpc.Sent, "빈 줄은 서버로 보내지 않는다.");
        }

        [UnityTest]
        public IEnumerator 클릭으로_포커스해도_게임플레이_입력이_잠긴다()
        {
            yield return null;

            // Enter 없이 EventSystem 이 직접 선택한 경우(= 마우스 클릭 경로)
            _eventSystem.SetSelectedGameObject(_inputField.gameObject);
            _inputField.ActivateInputField();
            yield return null;

            Assert.IsFalse(_actions.Player.enabled, "클릭으로 포커스했는데 WASD 가 살아 있으면 타이핑이 이동으로 샌다.");

            _inputField.onSubmit.Invoke("클릭 후 전송");
            Assert.AreEqual("클릭 후 전송", _grpc.LastSentMessage());
            Assert.IsTrue(_actions.Player.enabled);
        }

        private static object GetPrivate(object target, string field)
            => target.GetType().GetField(field, Private)?.GetValue(target);

        // ── 씬 구성 ─────────────────────────────

        private void BuildScene()
        {
            // 다른 픽스처가 남긴 EventSystem 이 있으면 EventSystem.current 가 그쪽을 가리켜
            // 선택 상태를 엉뚱한 곳에서 읽게 된다 — 내 것을 current 로 못 박는다(테스트 격리).
            foreach (var stray in Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include))
                Object.DestroyImmediate(stray.gameObject);

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem));
            _eventSystem = eventSystem.GetComponent<EventSystem>();
            _objects.Add(eventSystem);

            var canvasGo = new GameObject("Canvas", typeof(Canvas));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            _objects.Add(canvasGo);

            var panel = new GameObject("ChatPanel", typeof(RectTransform));
            panel.transform.SetParent(canvasGo.transform, false);

            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(panel.transform, false);
            var bubble = BuildBubble(content);

            _inputField = BuildInputField(panel.transform);

            var view = panel.AddComponent<ChatView>();
            SetPrivate(view, "logContent", content);
            SetPrivate(view, "bubbleTemplate", bubble);
            SetPrivate(view, "inputField", _inputField);
            SetPrivate(view, "_model", _model);
            SetPrivate(view, "_inputRouter", _router);
            _view = view;
        }

        /// <summary>말풍선 템플릿 최소 구성(Sender/Message)을 코드로 세운다.</summary>
        private static ChatBubbleView BuildBubble(Transform parent)
        {
            var go = new GameObject("ChatBubble", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var senderGo = new GameObject("Sender", typeof(RectTransform));
            senderGo.transform.SetParent(go.transform, false);
            var messageGo = new GameObject("Message", typeof(RectTransform));
            messageGo.transform.SetParent(go.transform, false);

            var bubble = go.AddComponent<ChatBubbleView>();
            SetPrivate(bubble, "sender", senderGo.AddComponent<TextMeshProUGUI>());
            SetPrivate(bubble, "message", messageGo.AddComponent<TextMeshProUGUI>());
            return bubble;
        }

        /// <summary>TMP_InputField 최소 구성(텍스트 컴포넌트 + 뷰포트)을 코드로 세운다.</summary>
        private static TMP_InputField BuildInputField(Transform parent)
        {
            var go = new GameObject("ChatInput", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var area = new GameObject("Text Area", typeof(RectTransform));
            area.transform.SetParent(go.transform, false);

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(area.transform, false);
            var text = textGo.AddComponent<TextMeshProUGUI>();

            var field = go.AddComponent<TMP_InputField>();
            field.textViewport = (RectTransform)area.transform;
            field.textComponent = text;
            return field;
        }

        private static void SetPrivate(object target, string field, object value)
        {
            var info = target.GetType().GetField(field, Private);
            Assert.IsNotNull(info, $"필드 {field} 를 찾지 못했다(이름이 바뀌었나?)");
            info.SetValue(target, value);
        }
    }
}
