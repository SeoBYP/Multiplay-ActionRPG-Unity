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

        public GameSessionConnector(
            IDungeonLobbyService lobbyService,
            ISocketSession       socketSession,
            AuthSession          authSession,
            IGameSceneManager    sceneManager)
        {
            _lobbyService  = lobbyService;
            _socketSession = socketSession;
            _authSession   = authSession;
            _sceneManager  = sceneManager;
        }

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
        private const int MaxJoinAttempts = 30;
        private static readonly TimeSpan JoinRetryDelay = TimeSpan.FromMilliseconds(500);

        private async UniTaskVoid ConnectAndLoadDungeonAsync(string ip, int port, long roomId)
        {
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
                        Debug.Log("[GameSessionConnector] 방 입장 완료 — Dungeon 씬 로드");
                        await _sceneManager.LoadSceneAsync("Dungeon", CancellationToken.None);
                        return;
                    }

                    var failState = _socketSession.State;
                    Debug.LogWarning($"[GameSessionConnector] 방 입장 실패 (시도 {attempt}/{MaxJoinAttempts}, state={failState}) — 재시도");
                    await _socketSession.DisconnectAsync(CancellationToken.None);

                    if (attempt < MaxJoinAttempts)
                        await UniTask.Delay(JoinRetryDelay);
                }

                Debug.LogError("[GameSessionConnector] 방 입장 실패 — 재시도 횟수 초과");
            }
            catch (OperationCanceledException)
            {
                Debug.Log("[GameSessionConnector] 소켓 연결 취소됨");
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameSessionConnector] 연결 실패: {e}");
            }
        }
    }
}
