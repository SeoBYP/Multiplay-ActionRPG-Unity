using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Network.Https.Interfaces;
using Game.Presentation.Chat;
using Game.System.Auth;
using GameServer.Grpc.Chat;

namespace Game.Tests.PlayMode.InGame
{
    /// <summary>
    /// 네트워크 없이 <see cref="ChatModel"/> 을 세우는 대역.
    ///
    /// HUD 통합 테스트의 관심사는 채팅이 아니지만 GameHud.prefab 을 통째로 생성하므로
    /// ChatView 의 주입은 성립해야 한다(VContainer 는 미등록 타입에서 즉시 던진다).
    /// </summary>
    internal static class HudChatTestDouble
    {
        /// <summary>스트림을 열지 않는 조용한 Model — 주입 충족용.</summary>
        public static ChatModel CreateModel()
            => new ChatModel(new SilentChatGrpcService(), new AuthSession(new NoopTokenStore()), new NoopInputContext(),
                             new Game.System.DungeonLobby.DungeonLobbySession(), null);

        /// <summary>
        /// 이미 "연결된" Model — <paramref name="grpc"/> 로 서버 메시지를 밀어 넣을 수 있다.
        /// 인증을 먼저 끝내 두므로 StartAsync 가 그 자리에서 스트림에 붙는다.
        /// </summary>
        public static ChatModel CreateConnectedModel(ControllableChatGrpcService grpc)
            => CreateConnectedModel(grpc, new NoopInputContext());

        /// <summary>방 소속을 테스트가 직접 바꿀 수 있게 로비 세션을 주입하는 변형.</summary>
        public static ChatModel CreateConnectedModel(ControllableChatGrpcService grpc, Game.System.DungeonLobby.DungeonLobbySession lobby)
            => CreateConnectedModel(grpc, new NoopInputContext(), lobby);

        /// <param name="inputContext">입력 점유를 실제로 확인하려면 진짜 <c>InputContext</c> 를 넘긴다.</param>
        public static ChatModel CreateConnectedModel(ControllableChatGrpcService grpc, Game.System.Input.IInputContext inputContext)
            => CreateConnectedModel(grpc, inputContext, new Game.System.DungeonLobby.DungeonLobbySession());

        public static ChatModel CreateConnectedModel(
            ControllableChatGrpcService grpc,
            Game.System.Input.IInputContext inputContext,
            Game.System.DungeonLobby.DungeonLobbySession lobby)
        {
            var auth = new AuthSession(new NoopTokenStore());
            auth.Update("header.payload.signature", "refresh", 0);

            var model = new ChatModel(grpc, auth, inputContext, lobby, socketSession: null);
            model.StartAsync(CancellationToken.None).Forget();
            return model;
        }

        private sealed class SilentChatGrpcService : IChatGrpcService
        {
            public UniTask ChatStreamAsync(IObservable<ChatClientMessage> outgoing, Action<ChatServerMessage> onMessage, CancellationToken ct = default)
                => UniTask.Never(ct);
        }

        private sealed class NoopTokenStore : ITokenStore
        {
            // 전역 PlayerPrefs 를 건드리지 않는다 — 테스트끼리 토큰을 공유하면 조용히 서로를 오염시킨다.
            public void Save(string accessToken, string refreshToken, long expiresAt) { }
            public bool TryLoad(out string accessToken, out string refreshToken, out long expiresAt)
            {
                accessToken = null; refreshToken = null; expiresAt = 0;
                return false;
            }
            public void Clear() { }
        }

        private sealed class NoopInputContext : Game.System.Input.IInputContext
        {
            public void EnterUi() { }
            public void ExitUi() { }
            public bool IsUiActive => false;
        }
    }

    /// <summary>테스트가 서버 역할을 대신하는 채팅 스트림.</summary>
    internal sealed class ControllableChatGrpcService : IChatGrpcService
    {
        public readonly global::System.Collections.Generic.List<ChatClientMessage> Sent = new global::System.Collections.Generic.List<ChatClientMessage>();

        private Action<ChatServerMessage> _onMessage;

        public UniTask ChatStreamAsync(IObservable<ChatClientMessage> outgoing, Action<ChatServerMessage> onMessage, CancellationToken ct = default)
        {
            _onMessage = onMessage;
            outgoing.Subscribe(new Relay(Sent.Add));
            return UniTask.Never(ct);
        }

        /// <summary>마지막으로 서버에 보낸 채팅 페이로드(없으면 null).</summary>
        public ChatPayload LastChatPayload()
        {
            for (int i = Sent.Count - 1; i >= 0; i--)
                if (Sent[i].PayloadCase == ChatClientMessage.PayloadOneofCase.Chat)
                    return Sent[i].Chat;
            return null;
        }

        /// <summary>마지막으로 서버에 보낸 채팅 본문(없으면 null).</summary>
        public string LastSentMessage()
        {
            for (int i = Sent.Count - 1; i >= 0; i--)
                if (Sent[i].PayloadCase == ChatClientMessage.PayloadOneofCase.Chat)
                    return Sent[i].Chat.Message;
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

        public void PushChat(long id, ChatType type, string sender, string message) =>
            _onMessage?.Invoke(new ChatServerMessage
            {
                Chat = new ChatMessageInfo
                {
                    MessageId = id, ChatType = type, SenderNickname = sender,
                    Message = message, SentAt = 0, TargetUserNickname = string.Empty,
                }
            });
    }
}
