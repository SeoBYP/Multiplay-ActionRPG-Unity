using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Network.Socket;
using Game.System.DungeonLobby;
using Game.System.Auth;
using Game.System.GameScene;
using UnityEngine;
using VContainer.Unity;

namespace Game.System.InGame
{
    /// <summary>
    /// SocketServer 준비 신호를 수신해 TCP 연결 → 인증 → 방 입장 → Dungeon 씬 로드 흐름을 주관한다.
    /// </summary>
    public class GameSessionConnector : IInitializable, IDisposable
    {
        private readonly IDungeonLobbyService _lobbyService;
        private readonly ISocketSession       _socketSession;
        private readonly AuthSession          _authSession;
        private readonly IGameSceneManager    _sceneManager;
        private readonly ISocketPacketState   _packetState;

        public GameSessionConnector(
            IDungeonLobbyService lobbyService,
            ISocketSession       socketSession,
            AuthSession          authSession,
            IGameSceneManager    sceneManager,
            ISocketPacketState   packetState)
        {
            _lobbyService  = lobbyService;
            _socketSession = socketSession;
            _authSession   = authSession;
            _sceneManager  = sceneManager;
            _packetState   = packetState;
        }

        /// <summary>전원 입장(S_GameStatus InProgress) 대기 타임아웃 — 초과 시 그대로 진행(무한 로딩 방지).</summary>
        private static readonly TimeSpan DungeonReadyTimeout = TimeSpan.FromSeconds(30);

        public void Initialize()
        {
            _lobbyService.OnGameSessionReady += HandleGameSessionReady;
            Debug.Log("[GameSessionConnector] Initialize 완료 — OnGameSessionReady 구독 시작");
        }

        public void Dispose()
        {
            _lobbyService.OnGameSessionReady -= HandleGameSessionReady;
        }

        private void HandleGameSessionReady(string ip, int port, long roomId)
        {
            Debug.Log($"[GameSessionConnector] GameSessionReady 수신 — ip={ip} port={port} roomId={roomId}");

            var state = _socketSession.State;
            if (state != SocketSessionState.Idle &&
                state != SocketSessionState.Disconnected &&
                state != SocketSessionState.Failed)
            {
                Debug.Log($"[GameSessionConnector] 이미 연결 중 (state={state}) — 중복 이벤트 무시");
                return;
            }

            ConnectAndLoadDungeonAsync(ip, port, roomId).Forget();
        }

        // SocketServer가 방을 생성하는 시점과 클라 접속 사이에 레이스가 있다.
        // 퇴장 후 재입장 시 auto-retrigger → Outbox → SocketServer 방 재생성까지 최대 10초 소요.
        // 방이 아직 없거나 연결이 끊겨도 재시도 루프가 커버한다.
        // 재시도 예산은 <b>서버 재접속 유예(Room.ReconnectGraceMs = 60s)보다 길어야</b> 한다.
        // 예전엔 30회×0.5s(≈30s)라 유예가 풀리기 전에 항상 먼저 포기했다(실측: 유예 만료 15:41:37 vs 포기 15:40:31).
        // 40회×2s ≈ 80s — 서버가 옛 세션을 정리할 시간을 주고도 남는다.
        private const int MaxJoinAttempts = 40;
        private static readonly TimeSpan JoinRetryDelay = TimeSpan.FromSeconds(2);

        /// <summary>재시도 경고를 매번 찍으면 콘솔이 40줄로 덮인다 — 첫 실패와 이후 N회마다만 남긴다.</summary>
        private const int JoinFailureLogInterval = 5;

        private async UniTaskVoid ConnectAndLoadDungeonAsync(string ip, int port, long roomId)
        {
            // 전원 입장(서버 S_GameStatus InProgress) 신호를 미리 잡아둔다.
            // 입장(JoinRoom) 전에 구독하므로 서버 브로드캐스트를 놓치지 않는다(TCS 래치).
            var readyTcs = new UniTaskCompletionSource();
            void OnDungeonReady()
            {
                Debug.Log("[GameSessionConnector] 전원 입장 신호 수신 (S_GameStatus InProgress) — 로딩 해제");
                readyTcs.TrySetResult();
            }
            _packetState.OnDungeonReady += OnDungeonReady;

            try
            {
                var info = new SocketConnectionInfo(ip, port, roomId, _authSession.UserId);

                for (var attempt = 1; attempt <= MaxJoinAttempts; attempt++)
                {
                    Debug.Log($"[GameSessionConnector] TCP 연결 시도 {attempt}/{MaxJoinAttempts} — ip={ip} port={port} userId={_authSession.UserId}");

                    await _socketSession.ConnectAsync(info, CancellationToken.None);
                    await _socketSession.JoinRoomAsync(CancellationToken.None);

                    // Joined / Failed 외에 Disconnected도 감지한다.
                    // JoinRoomAsync 전송 후 응답 대기 중 연결이 끊기면
                    // State = Disconnected가 되어 Joined/Failed 조건이 영원히 충족되지 않는다.
                    await UniTask.WaitUntil(
                        () => _socketSession.State == SocketSessionState.Joined
                           || _socketSession.State == SocketSessionState.Failed
                           || _socketSession.State == SocketSessionState.Disconnected);

                    if (_socketSession.State == SocketSessionState.Joined)
                    {
                        Debug.Log("[GameSessionConnector] 방 입장 완료 — Dungeon 씬 로드(전원 입장까지 Loading 유지)");
                        // 씬 로드 후 Loading을 띄운 채 전원 입장 대기 → 완료 시 Fader로 reveal.
                        await _sceneManager.LoadSceneAsync(
                            "Dungeon",
                            CancellationToken.None,
                            () => WaitForDungeonReadyAsync(readyTcs));
                        return;
                    }

                    var failState = _socketSession.State;
                    var reason = _socketSession.LastJoinFailureReason;
                    await _socketSession.DisconnectAsync(CancellationToken.None);

                    // 재시도로 바뀌지 않는 사유(배정 불일치 등)면 즉시 끝낸다 — 40번 헛돌 이유가 없다.
                    if (JoinFailurePolicy.IsTerminal(reason))
                    {
                        Debug.LogError($"[GameSessionConnector] 방 입장 불가 — 사유='{reason}' (재시도해도 바뀌지 않음, 중단)");
                        return;
                    }

                    if (attempt == 1 || attempt % JoinFailureLogInterval == 0)
                        Debug.LogWarning(
                            $"[GameSessionConnector] 방 입장 실패 (시도 {attempt}/{MaxJoinAttempts}, state={failState}, 사유='{reason ?? "미상"}') — 재시도");

                    if (attempt < MaxJoinAttempts)
                        await UniTask.Delay(JoinRetryDelay);
                }

                Debug.LogError($"[GameSessionConnector] 방 입장 실패 — 재시도 횟수 초과(마지막 사유='{_socketSession.LastJoinFailureReason ?? "미상"}')");
            }
            catch (OperationCanceledException)
            {
                Debug.Log("[GameSessionConnector] 소켓 연결 취소됨");
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameSessionConnector] 연결 실패: {e}");
            }
            finally
            {
                _packetState.OnDungeonReady -= OnDungeonReady;
            }
        }

        /// <summary>전원 입장 신호 또는 타임아웃까지 대기. 타임아웃 시 경고 후 진행(무한 로딩 방지).</summary>
        private static async UniTask WaitForDungeonReadyAsync(UniTaskCompletionSource ready)
        {
            Debug.Log("[GameSessionConnector] 전원 입장 대기 시작 — 'Loading' 유지");
            var winIndex = await UniTask.WhenAny(ready.Task, UniTask.Delay(DungeonReadyTimeout));
            if (winIndex != 0)
                Debug.LogWarning("[GameSessionConnector] 전원 입장 대기 타임아웃 — 그대로 진행");
            else
                Debug.Log("[GameSessionConnector] 전원 입장 확인 — 게임 시작(Fader 전환)");
        }
    }
}
