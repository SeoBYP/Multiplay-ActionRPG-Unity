using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.GUI;
using Game.GUI.OutGame;
using Game.Presentation.Chat;
using Game.System.DungeonLobby;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode.InGame
{
    /// <summary>
    /// 채팅 View. 세 가지를 본다:
    ///   ① GameHud.prefab 안의 ChatView·말풍선 배선이 살아 있는가(끊기면 채팅이 조용히 안 보인다)
    ///   ② 받은 줄이 실제 말풍선 행으로 그려지는가
    ///   ③ 채널 드롭다운 항목이 방 소속 여부에 따라 바뀌는가(Main = 전체·개인 / 방 = 방·개인)
    /// </summary>
    [TestFixture]
    public class ChatViewIntegrationTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        private readonly List<GameObject> _objects = new List<GameObject>();
        private AddressableInstance _hudInstance;

        [TearDown]
        public void TearDown()
        {
            _hudInstance?.Dispose();
            _hudInstance = null;

            foreach (var obj in _objects)
                if (obj != null) Object.Destroy(obj);
            _objects.Clear();
        }

        [UnityTest]
        public IEnumerator GameHud_프리팹의_ChatView_배선이_살아있다() => UniTask.ToCoroutine(async () =>
        {
            // 비활성 부모 아래에 만들어 MonoBehaviour 가 Start 되지 않게 한다(주입 없이 열어보기 위함).
            var holder = new GameObject("InactiveHolder");
            holder.SetActive(false);
            _objects.Add(holder);

            _hudInstance = await AddressableLoader.LoadAndInstantiateAsync(
                AddressKeys.UI.GameHud, holder.transform, CancellationToken.None);
            Assert.IsNotNull(_hudInstance, "GameHud.prefab 로드 실패");

            var view = _hudInstance.GameObject.GetComponentInChildren<ChatView>(true);
            Assert.IsNotNull(view, "GameHud.prefab 에 ChatView 가 없다(ChatPanel 배선 확인).");

            Assert.IsNotNull(GetPrivate(view, "logContent"),      "logContent 미배선 — 말풍선을 담을 곳이 없다.");
            Assert.IsNotNull(GetPrivate(view, "bubbleTemplate"),  "bubbleTemplate 미배선 — 채팅이 화면에 안 그려진다.");
            Assert.IsNotNull(GetPrivate(view, "inputField"),      "inputField 미배선 — 입력할 수 없다.");
            Assert.IsNotNull(GetPrivate(view, "channelDropdown"), "channelDropdown 미배선 — 채널을 고를 수 없다.");

            var bubble = (ChatBubbleView)GetPrivate(view, "bubbleTemplate");
            Assert.IsNotNull(GetPrivate(bubble, "sender"),  "말풍선 Sender 미배선");
            Assert.IsNotNull(GetPrivate(bubble, "message"), "말풍선 Message 미배선");
        });

        [UnityTest]
        public IEnumerator 수신한_채팅이_채널_표기와_함께_말풍선으로_그려진다()
        {
            var grpc = new ControllableChatGrpcService();
            var view = BuildView(HudChatTestDouble.CreateConnectedModel(grpc), new DungeonLobbySession(), out var content);
            yield return null; // Start → 구독

            grpc.PushChat(1, GameServer.Grpc.Chat.ChatType.Global, "철수", "전체다");
            grpc.PushChat(2, GameServer.Grpc.Chat.ChatType.Room, "영희", "방이다");
            yield return null;

            var rows = VisibleRows(content);
            Assert.AreEqual(2, rows.Count, "받은 줄 수만큼 말풍선이 보여야 한다.");
            StringAssert.Contains("철수", rows[0].sender);
            StringAssert.Contains("전체다", rows[0].message);
            StringAssert.Contains("[방]", rows[1].sender);
            StringAssert.Contains("방이다", rows[1].message);
        }

        [UnityTest]
        public IEnumerator 남의_리치텍스트_태그는_서식으로_해석되지_않는다()
        {
            var grpc = new ControllableChatGrpcService();
            var view = BuildView(HudChatTestDouble.CreateConnectedModel(grpc), new DungeonLobbySession(), out var content);
            yield return null;

            grpc.PushChat(1, GameServer.Grpc.Chat.ChatType.Global, "철수", "<color=#FF0000>빨강</color>");
            yield return null;

            var rows = VisibleRows(content);
            Assert.AreEqual(1, rows.Count);
            StringAssert.Contains("<color=#FF0000>빨강</color>", rows[0].message, "본문은 태그까지 그대로 글자로 남아야 한다.");
            Assert.IsFalse(rows[0].messageRichText, "말풍선 본문은 richText 를 꺼서 태그 주입을 원천 차단한다.");
        }

        [UnityTest]
        public IEnumerator 방_밖에서는_드롭다운이_전체와_개인이다()
        {
            var grpc = new ControllableChatGrpcService();
            var view = BuildView(HudChatTestDouble.CreateConnectedModel(grpc), new DungeonLobbySession(), out _);
            yield return null;

            var dropdown = (TMP_Dropdown)GetPrivate(view, "channelDropdown");
            Assert.AreEqual(2, dropdown.options.Count);
            Assert.AreEqual("전체", dropdown.options[0].text);
            Assert.AreEqual("개인", dropdown.options[1].text);
        }

        [UnityTest]
        public IEnumerator 방에_들어가면_드롭다운이_방과_개인으로_바뀐다()
        {
            var grpc = new ControllableChatGrpcService();
            var lobby = new DungeonLobbySession();
            var view = BuildView(HudChatTestDouble.CreateConnectedModel(grpc, lobby), lobby, out _);
            yield return null;

            var dropdown = (TMP_Dropdown)GetPrivate(view, "channelDropdown");
            Assert.AreEqual("전체", dropdown.options[0].text, "입장 전에는 전체여야 한다.");

            lobby.SetRoom(new GameServer.Grpc.DungeonLobby.RoomInfo { RoomId = 3 });
            yield return null; // Update 에서 변화 감지 → 항목 재구성

            Assert.AreEqual(2, dropdown.options.Count);
            Assert.AreEqual("방", dropdown.options[0].text, "방에 속하면 서버가 방 채팅으로 보내므로 표기도 '방' 이어야 한다.");
            Assert.AreEqual("개인", dropdown.options[1].text);
        }

        [UnityTest]
        public IEnumerator 개인을_고르고_보내면_첫_단어가_받는_사람이_된다()
        {
            var grpc = new ControllableChatGrpcService();
            var view = BuildView(HudChatTestDouble.CreateConnectedModel(grpc), new DungeonLobbySession(), out _);
            yield return null;

            var dropdown = (TMP_Dropdown)GetPrivate(view, "channelDropdown");
            dropdown.value = 1; // 개인

            var input = (TMP_InputField)GetPrivate(view, "inputField");
            SetPrivate(view, "_typing", true); // 포커스 상태를 만든 것과 같다
            input.onSubmit.Invoke("홍길동 어디야");

            var sent = grpc.LastChatPayload();
            Assert.IsNotNull(sent, "개인 채널로 보낸 메시지가 없다.");
            Assert.AreEqual("홍길동", sent.TargetUserNickname);
            Assert.AreEqual("어디야", sent.Message);
        }

        // ── 헬퍼 ────────────────────────────────

        /// <summary>프리팹 없이 ChatView + 말풍선 템플릿 + 드롭다운을 코드로 세운다.</summary>
        /// <param name="lobby">Model 이 이미 물고 있는 것과 같은 세션(테스트가 방 입장을 흉내 낼 때 쓴다).</param>
        private ChatView BuildView(ChatModel model, DungeonLobbySession lobby, out RectTransform content)
        {
            var canvas = new GameObject("Canvas", typeof(Canvas));
            _objects.Add(canvas);

            var panel = new GameObject("ChatPanel", typeof(RectTransform));
            panel.transform.SetParent(canvas.transform, false);

            content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(panel.transform, false);

            var template = BuildBubble(content);
            var input = BuildInputField(panel.transform);
            var dropdown = BuildDropdown(panel.transform);

            var view = panel.AddComponent<ChatView>();
            SetPrivate(view, "logContent", content);
            SetPrivate(view, "bubbleTemplate", template);
            SetPrivate(view, "inputField", input);
            SetPrivate(view, "channelDropdown", dropdown);
            SetPrivate(view, "_model", model);
            return view;
        }

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

        private static TMP_InputField BuildInputField(Transform parent)
        {
            var go = new GameObject("ChatInput", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var area = new GameObject("Text Area", typeof(RectTransform));
            area.transform.SetParent(go.transform, false);
            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(area.transform, false);

            var field = go.AddComponent<TMP_InputField>();
            field.textViewport = (RectTransform)area.transform;
            field.textComponent = textGo.AddComponent<TextMeshProUGUI>();
            return field;
        }

        private static TMP_Dropdown BuildDropdown(Transform parent)
        {
            var go = new GameObject("ChannelDropdown", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);

            var dropdown = go.AddComponent<TMP_Dropdown>();
            dropdown.captionText = labelGo.AddComponent<TextMeshProUGUI>();
            return dropdown;
        }

        private static List<(string sender, string message, bool messageRichText)> VisibleRows(RectTransform content)
        {
            var rows = new List<(string, string, bool)>();
            foreach (var bubble in content.GetComponentsInChildren<ChatBubbleView>(true))
            {
                if (!bubble.gameObject.activeSelf) continue; // 템플릿 원본과 남는 행은 숨어 있다
                var sender  = (TextMeshProUGUI)GetPrivate(bubble, "sender");
                var message = (TextMeshProUGUI)GetPrivate(bubble, "message");
                rows.Add((sender.text, message.text, message.richText));
            }
            return rows;
        }

        private static object GetPrivate(object target, string field)
            => target.GetType().GetField(field, Private)?.GetValue(target);

        private static void SetPrivate(object target, string field, object value)
        {
            var info = target.GetType().GetField(field, Private);
            Assert.IsNotNull(info, $"필드 {field} 를 찾지 못했다(이름이 바뀌었나?)");
            info.SetValue(target, value);
        }
    }
}
