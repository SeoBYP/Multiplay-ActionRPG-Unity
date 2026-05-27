using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Network.Socket;
using Game.System.DungeonLobby;
using Script.System.Auth;
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

        private async UniTaskVoid ConnectAndLoadDungeonAsync(string ip, int port, long roomId)
        {
            try
            {
                Debug.Log($"[GameSessionConnector] TCP 연결 시도 — ip={ip} port={port} userId={_authSession.UserId}");
                var info = new SocketConnectionInfo(ip, port, roomId, _authSession.UserId);

                await _socketSession.ConnectAsync(info, CancellationToken.None);
                Debug.Log($"[GameSessionConnector] TCP 연결 완료 — {ip}:{port}");

                await _socketSession.JoinRoomAsync(CancellationToken.None);
                await UniTask.WaitUntil(
                    () => _socketSession.State == SocketSessionState.Joined
                       || _socketSession.State == SocketSessionState.Failed);

                if (_socketSession.State == SocketSessionState.Failed)
                {
                    Debug.LogError("[GameSessionConnector] 방 입장 실패");
                    return;
                }
                Debug.Log("[GameSessionConnector] 방 입장 완료 — Dungeon 씬 로드");

                await SceneManager.LoadSceneAsync("Dungeon");
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
