using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Network.Https.Interfaces;
using Game.Presentation.Chat;
using Game.System.Auth;
using GameServer.Grpc.Chat;
using NUnit.Framework;
using R3;

namespace Game.Tests.EditMode.Presentation
{
    /// <summary>
    /// 채팅 Model 계약. 네트워크는 Fake 로 끊고 **입력 파싱 · 수신 버퍼 · 전송 페이로드**만 본다.
    /// 실제 서버 왕복은 PlayMode `ChatE2ETests`(Docker) 담당.
    /// </summary>
    [TestFixture]
    public class ChatModelTests
    {
        private FakeChatGrpcService _grpc;
        private Game.System.DungeonLobby.DungeonLobbySession _lobby;
        private ChatModel _model;

        [SetUp]
        public void SetUp()
        {
            var auth = new AuthSession(new FakeTokenStore());
            auth.Update("header.payload.signature", "refresh", 0); // 인증 완료 신호

            _grpc   = new FakeChatGrpcService();
            _lobby  = new Game.System.DungeonLobby.DungeonLobbySession();
            // 입력 점유·소켓은 이 테스트의 관심사가 아니다(널 가드로 무해). 방 소속은 로비 세션으로 만든다.
            _model  = new ChatModel(_grpc, auth, inputContext: null, lobbySession: _lobby, socketSession: null);
            _model.StartAsync(CancellationToken.None).Forget(); // 인증이 이미 끝나 동기적으로 스트림에 붙는다
        }

        [TearDown]
        public void TearDown() => _model.Dispose();

        // ── 전송 ────────────────────────────────

        [Test]
        public void 일반_메시지는_대상없이_전송된다()
        {
            Assert.IsTrue(_model.Send("안녕하세요"));

            var sent = _grpc.LastChat();
            Assert.AreEqual("안녕하세요", sent.Message);
            Assert.IsEmpty(sent.TargetUserNickname); // 비어 있으면 서버가 전체/방을 스스로 고른다
        }

        [Test]
        public void 슬래시w_는_귓속말_대상을_붙여_전송된다()
        {
            Assert.IsTrue(_model.Send("/w 홍길동 어디야"));

            var sent = _grpc.LastChat();
            Assert.AreEqual("홍길동", sent.TargetUserNickname);
            Assert.AreEqual("어디야", sent.Message);
        }

        [Test]
        public void 귓속말_내용에_공백이_있어도_통째로_전송된다()
        {
            Assert.IsTrue(_model.Send("/w 홍길동 지금 던전 갈래?"));
            Assert.AreEqual("지금 던전 갈래?", _grpc.LastChat().Message);
        }

        [Test]
        public void 빈_입력은_전송하지_않는다()
        {
            Assert.IsFalse(_model.Send("   "));
            Assert.IsEmpty(_grpc.Sent);
        }

        [Test]
        public void 귓속말_대상만_있고_내용이_없으면_전송하지_않는다()
        {
            Assert.IsFalse(_model.Send("/w 홍길동"));
            Assert.IsEmpty(_grpc.Sent);
        }

        [Test]
        public void 알수없는_슬래시_명령은_일반_메시지로_전송된다()
        {
            // 명령을 삼켜 침묵하면 사용자는 왜 안 갔는지 알 수 없다. 그냥 말로 보낸다.
            Assert.IsTrue(_model.Send("/모르는명령"));
            Assert.AreEqual("/모르는명령", _grpc.LastChat().Message);
        }

        [Test]
        public void 서버_상한을_넘는_메시지는_전송하지_않는다()
        {
            Assert.IsFalse(_model.Send(new string('가', ChatModel.MaxMessageLength + 1)));
            Assert.IsEmpty(_grpc.Sent);
        }

        // ── 채널(드롭다운이 읽는 값) ─────────────

        [Test]
        public void 방에_속하지_않으면_일반채널은_전체다()
        {
            Assert.IsFalse(_model.IsInRoom);
        }

        [Test]
        public void 방에_들어가면_일반채널이_방으로_바뀐다()
        {
            _lobby.SetRoom(new GameServer.Grpc.DungeonLobby.RoomInfo { RoomId = 7 });
            Assert.IsTrue(_model.IsInRoom, "대기실 입장 시점부터 서버는 방 채팅으로 취급한다.");
        }

        [Test]
        public void 개인채널이면_첫_단어가_받는_사람이다()
        {
            Assert.IsTrue(_model.Send("홍길동 어디야", whisperMode: true));

            var sent = _grpc.LastChat();
            Assert.AreEqual("홍길동", sent.TargetUserNickname);
            Assert.AreEqual("어디야", sent.Message);
        }

        [Test]
        public void 개인채널에서_받는_사람만_쓰면_전송하지_않는다()
        {
            Assert.IsFalse(_model.Send("홍길동", whisperMode: true));
            Assert.IsEmpty(_grpc.Sent);
        }

        [Test]
        public void 개인채널에서도_슬래시w_문법은_그대로_동작한다()
        {
            Assert.IsTrue(_model.Send("/w 영희 안녕", whisperMode: true));
            Assert.AreEqual("영희", _grpc.LastChat().TargetUserNickname);
            Assert.AreEqual("안녕", _grpc.LastChat().Message);
        }

        // ── 수신 ────────────────────────────────

        [Test]
        public void 수신한_채팅은_최근목록과_스트림에_모두_들어간다()
        {
            var observed = new List<ChatLine>();
            using var subscription = _model.OnLine.Subscribe(line => observed.Add(line));

            _grpc.PushChat(7, ChatType.Global, "철수", "하이");

            Assert.AreEqual(1, observed.Count);
            Assert.AreEqual("하이", observed[0].Text);
            Assert.AreEqual(ChatChannel.Global, observed[0].Channel);
            Assert.AreEqual(1, _model.Recent.Count);
            Assert.AreEqual("철수", _model.Recent[0].Sender);
        }

        [Test]
        public void 방채팅과_귓속말은_각자_채널로_구분된다()
        {
            _grpc.PushChat(1, ChatType.Room, "철수", "방이야");
            _grpc.PushChat(2, ChatType.Whisper, "영희", "귓이야");

            Assert.AreEqual(ChatChannel.Room, _model.Recent[0].Channel);
            Assert.AreEqual(ChatChannel.Whisper, _model.Recent[1].Channel);
        }

        [Test]
        public void 시스템_공지는_System_채널로_들어온다()
        {
            _grpc.PushNotice("서버 점검 예정");

            Assert.AreEqual(ChatChannel.System, _model.Recent[0].Channel);
            Assert.AreEqual("서버 점검 예정", _model.Recent[0].Text);
        }

        [Test]
        public void 최근목록은_상한을_넘지_않고_오래된_줄부터_버린다()
        {
            for (int i = 1; i <= ChatModel.MaxLines + 5; i++)
                _grpc.PushChat(i, ChatType.Global, "철수", $"메시지{i}");

            Assert.AreEqual(ChatModel.MaxLines, _model.Recent.Count);
            Assert.AreEqual("메시지6", _model.Recent[0].Text); // 앞의 5줄이 밀려났다
        }

        [Test]
        public void 수신하면_마지막_메시지ID가_갱신된다()
        {
            // 재연결 시 ReconnectPayload 에 실려 밀린 메시지를 되받는 근거값.
            _grpc.PushChat(42, ChatType.Global, "철수", "하이");
            Assert.AreEqual(42, _model.LastMessageId);

            _grpc.PushNotice("공지"); // 공지는 채팅 ID 를 되돌리지 않는다
            Assert.AreEqual(42, _model.LastMessageId);
        }

        // ── Fakes ───────────────────────────────

        private sealed class FakeTokenStore : ITokenStore
        {
            public void Save(string accessToken, string refreshToken, long expiresAt) { }
            public bool TryLoad(out string accessToken, out string refreshToken, out long expiresAt)
            {
                accessToken = null; refreshToken = null; expiresAt = 0;
                return false;
            }
            public void Clear() { }
        }

        private sealed class FakeChatGrpcService : IChatGrpcService
        {
            public readonly List<ChatClientMessage> Sent = new List<ChatClientMessage>();

            private Action<ChatServerMessage> _onMessage;
            private UniTaskCompletionSource _stream;

            public UniTask ChatStreamAsync(IObservable<ChatClientMessage> outgoing, Action<ChatServerMessage> onMessage, CancellationToken ct = default)
            {
                _onMessage = onMessage;
                outgoing.Subscribe(new Relay(Sent.Add));
                _stream = new UniTaskCompletionSource();
                return _stream.Task; // 끊기 전까지 열려 있는 스트림
            }

            public void PushChat(long id, ChatType type, string sender, string message) =>
                _onMessage?.Invoke(new ChatServerMessage
                {
                    Chat = new ChatMessageInfo
                    {
                        MessageId = id, ChatType = type, SenderNickname = sender,
                        Message = message, SentAt = 0, TargetUserNickname = string.Empty,
                    }
                });

            public void PushNotice(string message) =>
                _onMessage?.Invoke(new ChatServerMessage
                {
                    Notice = new SystemNotice { NoticeId = 1, Message = message, SentAt = 0 }
                });

            public ChatPayload LastChat()
            {
                for (int i = Sent.Count - 1; i >= 0; i--)
                    if (Sent[i].PayloadCase == ChatClientMessage.PayloadOneofCase.Chat)
                        return Sent[i].Chat;
                return null;
            }

            private sealed class Relay : IObserver<ChatClientMessage>
            {
                private readonly Action<ChatClientMessage> _onNext;
                public Relay(Action<ChatClientMessage> onNext) => _onNext = onNext;
                public void OnNext(ChatClientMessage value) => _onNext(value);
                public void OnError(Exception error) { }
                public void OnCompleted() { }
            }
        }
    }
}
