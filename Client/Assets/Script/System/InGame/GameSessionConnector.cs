using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Network.Socket;
using Game.System.DungeonLobby;
using Game.System.Auth;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer.Unity;

namespace Game.System.InGame
{
    /// <summary>
    /// SocketServer 준비 신호를 수신해 TCP 연결 → 인증 → 방 입장 → Dungeon 씬 로드 흐름을 주관한다.
    /// </summary>
    public class GameSessionConnector : IInitializable, IDisposable
    {
        private readonly IDungeonLobbyService _lobbyService;
        private readonly ISocketSession _socketSession;
        private readonly AuthSession _authSession;

        public GameSessionConnector(
            IDungeonLobbyService lobbyService,
            ISocketSession socketSession,
            AuthSession authSession)
        {
            _lobbyService   = lobbyService;
            _socketSession  = socketSession;
            _authSession    = authSession;
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

        // SocketServer가 GameStartRequested를 소비해 방을 생성하는 시점과
        // 클라가 GameSessionReady를 받아 접속하는 시점 사이에 레이스가 있다.
        // 방이 아직 없으면 서버가 S_PlayerJoined{Success=false}("Room not found")를 보내므로,
        // 단발 시도로 끝내지 않고 방 생성이 끝날 때까지 짧게 재접속·재입장한다.
        private const int MaxJoinAttempts = 10;
        private static readonly TimeSpan JoinRetryDelay = TimeSpan.FromMilliseconds(300);

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
                    await UniTask.WaitUntil(
                        () => _socketSession.State == SocketSessionState.Joined
                           || _socketSession.State == SocketSessionState.Failed);

                    if (_socketSession.State == SocketSessionState.Joined)
                    {
                        Debug.Log("[GameSessionConnector] 방 입장 완료 — Dungeon 씬 로드");
                        await SceneManager.LoadSceneAsync("Dungeon");
                        return;
                    }

                    // 방 생성 전 접속 레이스로 추정 — 연결을 닫고 잠시 후 재시도한다.
                    Debug.LogWarning($"[GameSessionConnector] 방 입장 실패 (시도 {attempt}/{MaxJoinAttempts}) — 재시도");
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
