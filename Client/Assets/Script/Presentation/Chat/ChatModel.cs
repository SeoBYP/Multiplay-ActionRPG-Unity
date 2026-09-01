using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Network.Https.Interfaces;
using Game.Network.Socket;
using Game.System.Auth;
using Game.System.DungeonLobby;
using Game.System.Input;
using GameServer.Grpc.Chat;
using R3;
using UnityEngine;
using VContainer.Unity;

namespace Game.Presentation.Chat
{
    /// <summary>
    /// 채팅 MVI Model. **루트 스코프 Singleton** — 스트림이 씬 수명에 묶이면
    /// Main↔Dungeon 왕복마다 재연결되고 그 사이 메시지를 놓친다.
    /// HUD(ChatView)는 씬마다 새로 생기지만 <see cref="Recent"/> 로 지난 로그를 즉시 복원한다.
    ///
    /// 채널(전체/방/귓속말)은 **서버가 정한다** — 방에 속해 있으면 방, 아니면 전체.
    /// 클라가 보내는 것은 본문과 (귓속말일 때만) 대상 닉네임뿐이다.
    ///
    /// System 서비스 계층을 따로 두지 않은 이유: 채팅의 소비자는 이 화면 하나뿐이라
    /// 감쌀 대상이 없다(unity-client.md "불필요한 추상화 금지"). 비UI 소비자가 생기면 그때 분리한다.
    /// </summary>
    public sealed class ChatModel : IAsyncStartable, IDisposable
    {
        public const int MaxLines = 100;
        public const int MaxMessageLength = 1000; // 서버 ChatMessage.MaxMessageLength 와 같은 값

        private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(3);

        private readonly IChatGrpcService _chat;
        private readonly AuthSession _auth;
        private readonly IInputContext _inputContext;
        private readonly DungeonLobbySession _lobbySession;
        private readonly ISocketSession _socketSession;

        private readonly Outbox _outbox = new Outbox();
        private readonly List<ChatLine> _lines = new List<ChatLine>(MaxLines);
        private readonly Subject<ChatLine> _lineAdded = new Subject<ChatLine>();

        private CancellationTokenSource _cts;
        private bool _connected;
        private bool _disposed;

        /// <param name="inputContext">
        /// 선택 인자로 두지 않는다 — VContainer 는 C# 기본값을 채워주지 않아,
        /// 기본값이 있으면 "등록만 빠지면 런타임 첫 씬에서 컨테이너가 통째로 죽는" 형태가 된다.
        /// </param>
        public ChatModel(
            IChatGrpcService chat,
            AuthSession auth,
            IInputContext inputContext,
            DungeonLobbySession lobbySession,
            ISocketSession socketSession)
        {
            _chat          = chat;
            _auth          = auth;
            _inputContext  = inputContext;
            _lobbySession  = lobbySession;
            _socketSession = socketSession;
        }

        /// <summary>
        /// 지금 보내는 말이 **방 채팅으로 갈지** — 서버 규칙(dungeon_room_players 소속)을 클라에서 그대로 비춘 값.
        ///
        /// 두 경로를 모두 봐야 한다: 대기실은 `DungeonLobbySession` 이 방을 들고 있고,
        /// **던전에 들어가는 순간 그 세션은 비워지지만**(GameSessionEvent → ClearRoom) 서버 쪽 방 소속은 유지된다
        /// — 그 구간은 소켓이 Joined 인 것으로 판별한다. 한쪽만 보면 던전에서 채널 표기가 틀어진다.
        /// </summary>
        public bool IsInRoom =>
            (_lobbySession != null && _lobbySession.IsInRoom) ||
            (_socketSession != null && _socketSession.State == SocketSessionState.Joined);

        /// <summary>새 줄이 도착할 때마다 발화. 기존 줄은 <see cref="Recent"/> 로 읽는다.</summary>
        public Observable<ChatLine> OnLine => _lineAdded;

        /// <summary>최근 <see cref="MaxLines"/> 줄(오래된 것부터). HUD 재생성 시 로그 복원용.</summary>
        public IReadOnlyList<ChatLine> Recent => _lines;

        /// <summary>마지막으로 받은 채팅 메시지 ID — 재연결 시 밀린 메시지를 되받는 기준.</summary>
        public long LastMessageId { get; private set; }

        public bool IsConnected => _connected;

        // UI 점유(Player 맵 OFF). GUI 는 IInputContext(System)를 직접 참조할 수 없어 Model 이 대신 노출한다.
        public void BeginUiCapture() => _inputContext?.EnterUi();
        public void EndUiCapture()   => _inputContext?.ExitUi();

        public async UniTask StartAsync(CancellationToken ct)
        {
            await _auth.AuthenticatedAsync(); // 로그인 전에는 스트림을 열 수 없다(Unauthenticated)
            if (_disposed) return;

            _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            StreamLoopAsync(_cts.Token).Forget();
        }

        /// <summary>입력 한 줄 전송. 보낼 것이 없으면 false(입력창은 그대로 둔다).</summary>
        /// <param name="whisperMode">
        /// 채널 드롭다운에서 '개인'을 고른 상태. 이때는 **첫 단어가 받는 사람**이다("홍길동 어디야").
        /// </param>
        public bool Send(string input, bool whisperMode = false)
        {
            if (!TryBuildPayload(input, whisperMode, out var message, out var target))
                return false;

            _outbox.Publish(new ChatClientMessage
            {
                Chat = new ChatPayload { Message = message, TargetUserNickname = target }
            });
            return true;
        }

        /// <summary>
        /// 입력 문자열 → 전송 페이로드.
        /// <c>/w 닉 내용</c> 만 명령으로 해석하고, 나머지 <c>/xxx</c> 는 그냥 말로 보낸다
        /// (모르는 명령을 조용히 삼키면 왜 안 갔는지 알 수 없다).
        /// </summary>
        public static bool TryBuildPayload(string input, out string message, out string target)
            => TryBuildPayload(input, false, out message, out target);

        /// <param name="whisperMode">'개인' 채널 선택 상태 — 명령어 없이 첫 단어를 대상으로 읽는다.</param>
        public static bool TryBuildPayload(string input, bool whisperMode, out string message, out string target)
        {
            message = null;
            target  = null;

            if (string.IsNullOrWhiteSpace(input))
                return false;

            var text = input.Trim();

            if (whisperMode && !IsWhisperCommand(text))
            {
                // "홍길동 어디야" → 대상=홍길동 / 본문=어디야. 받는 사람만 쓰고 끝나면 보내지 않는다.
                var whisperParts = text.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
                if (whisperParts.Length < 2) return false;

                target  = whisperParts[0];
                message = whisperParts[1].Trim();
                return message.Length > 0 && message.Length <= MaxMessageLength;
            }

            if (IsWhisperCommand(text))
            {
                var parts = text.Split(new[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3) return false;      // 대상만 있고 할 말이 없다

                target  = parts[1];
                message = parts[2].Trim();
            }
            else
            {
                target  = string.Empty;                  // 비우면 서버가 전체/방을 스스로 고른다
                message = text;
            }

            return message.Length > 0 && message.Length <= MaxMessageLength;
        }

        private static bool IsWhisperCommand(string text)
        {
            if (text.Length == 0 || text[0] != '/') return false;

            var head = text.Split(' ')[0].ToLowerInvariant();
            return head == "/w" || head == "/whisper" || head == "/귓";
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            _lineAdded.Dispose();
        }

        // ── 스트림 ──────────────────────────────

        /// <summary>
        /// 끊기면 <see cref="RetryDelay"/> 뒤 재연결하고, 이전에 받은 마지막 ID 로 밀린 메시지를 요청한다.
        /// 스트림 자체가 세션 수명(로그인~종료)이라 여기서 끝내면 채팅이 조용히 죽는다.
        /// </summary>
        private async UniTaskVoid StreamLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    // 구독 전에 넣어도 Outbox 가 들고 있다가 연결되면 흘려보낸다.
                    if (LastMessageId > 0)
                        _outbox.Publish(new ChatClientMessage
                        {
                            Reconnect = new ReconnectPayload { LastMessageId = LastMessageId }
                        });

                    _connected = true;
                    await _chat.ChatStreamAsync(_outbox, OnServerMessage, ct);
                }
                catch (OperationCanceledException)
                {
                    // 정상 종료(로그아웃/앱 종료)
                }
                catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.Cancelled)
                {
                    // gRPC 레이어의 정상 취소
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ChatModel] 채팅 스트림 끊김 — {RetryDelay.TotalSeconds}초 뒤 재연결: {ex.Message}");
                }
                finally
                {
                    _connected = false;
                }

                if (ct.IsCancellationRequested) break;

                await UniTask.Delay(RetryDelay, cancellationToken: ct).SuppressCancellationThrow();
            }
        }

        /// <summary>
        /// 서버 → 클라 1건. 호출 스레드는 스트림 루프를 시작한 스레드(메인)의 동기화 컨텍스트로 돌아온다.
        /// </summary>
        private void OnServerMessage(ChatServerMessage message)
        {
            switch (message.PayloadCase)
            {
                case ChatServerMessage.PayloadOneofCase.Chat:
                    if (message.Chat.MessageId > LastMessageId)
                        LastMessageId = message.Chat.MessageId;
                    Append(ChatLine.FromChat(message.Chat));
                    break;

                case ChatServerMessage.PayloadOneofCase.Notice:
                    Append(ChatLine.FromNotice(message.Notice));
                    break;
            }
        }

        private void Append(ChatLine line)
        {
            _lines.Add(line);
            if (_lines.Count > MaxLines)
                _lines.RemoveRange(0, _lines.Count - MaxLines);

            _lineAdded.OnNext(line);
        }

        /// <summary>
        /// 송신 채널. 생성된 <c>ChatGrpcService</c> 가 요구하는 <see cref="IObservable{T}"/> 계약을 만족시키면서,
        /// **연결 전에 넣은 메시지를 버리지 않는다**(구독 시점에 밀어낸다) — 재연결 요청이 여기 의존한다.
        /// </summary>
        private sealed class Outbox : IObservable<ChatClientMessage>
        {
            private const int MaxPending = 32; // 서버가 오래 죽어 있어도 큐가 무한히 자라지 않게

            private readonly List<IObserver<ChatClientMessage>> _observers = new List<IObserver<ChatClientMessage>>();
            private readonly Queue<ChatClientMessage> _pending = new Queue<ChatClientMessage>();

            public IDisposable Subscribe(IObserver<ChatClientMessage> observer)
            {
                _observers.Add(observer);

                while (_pending.Count > 0)
                    observer.OnNext(_pending.Dequeue());

                return new Subscription(_observers, observer);
            }

            public void Publish(ChatClientMessage message)
            {
                if (_observers.Count == 0)
                {
                    if (_pending.Count >= MaxPending)
                        _pending.Dequeue();
                    _pending.Enqueue(message);
                    return;
                }

                foreach (var observer in _observers.ToArray())
                    observer.OnNext(message);
            }

            private sealed class Subscription : IDisposable
            {
                private readonly List<IObserver<ChatClientMessage>> _observers;
                private readonly IObserver<ChatClientMessage> _observer;

                public Subscription(List<IObserver<ChatClientMessage>> observers, IObserver<ChatClientMessage> observer)
                {
                    _observers = observers;
                    _observer  = observer;
                }

                public void Dispose() => _observers.Remove(_observer);
            }
        }
    }
}
