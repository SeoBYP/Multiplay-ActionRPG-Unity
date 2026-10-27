using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Network.Socket.Packets;
using UnityEngine;

namespace Game.Network.Socket
{
    /// <summary>
    /// 소켓 연결 상태 머신을 관리하는 상위 세션 구현체.
    /// </summary>
    public class SocketSession : ISocketSession
    {
        private readonly ISocketConnector _connector;
        private readonly ISocketPacketDispatcher _dispatcher;

        private SocketConnectionInfo _connectionInfo;
        private CancellationTokenSource _sessionCts;

        // true 면 의도적 종료(DisconnectAsync) — 수신 루프 종료 시 OnDisconnected 를 발화하지 않는다.
        private bool _intentionalDisconnect;

        public SocketSessionState State { get; private set; } = SocketSessionState.Idle;

        /// <summary>직전 입장 거절 사유(서버가 보낸 문구). 성공하면 null.</summary>
        public string LastJoinFailureReason { get; private set; }

        /// <inheritdoc/>
        public event Action OnDisconnected;

        /// <summary>
        /// keep-alive 핑 주기. 서버 유휴 타임아웃(방 60s·로비 30s, HeartBeatService)보다 충분히 짧아야 한다.
        /// 클라가 움직이지 않으면 C_Move 가 안 나가 서버가 무응답으로 퇴장시키므로, 이 핑이 연결을 유지한다.
        /// DI(ctor)로 주입하지 않는다(VContainer 가 TimeSpan 미해소) — 테스트는 이 프로퍼티를 직접 설정.
        /// </summary>
        public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(15);

        public SocketSession(
            ISocketConnector connector,
            ISocketPacketDispatcher dispatcher)
        {
            _connector = connector;
            _dispatcher = dispatcher;
        }

        public async UniTask ConnectAsync(SocketConnectionInfo connectionInfo, CancellationToken ct)
        {
            if (State != SocketSessionState.Idle &&
                State != SocketSessionState.Disconnected &&
                State != SocketSessionState.Failed)
            {
                throw new InvalidOperationException("SocketSession is already active.");
            }

            try
            {
                _connectionInfo = connectionInfo;
                CancelAndDisposeSessionToken();
                _sessionCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

                State = SocketSessionState.Connecting;
                _intentionalDisconnect = false; // 새 세션 — 비정상 끊김 발화 가능 상태로 리셋

                await _connector.ConnectAsync(connectionInfo.Host, connectionInfo.Port, _sessionCts.Token);

                State = SocketSessionState.Connected;

                RunReceiveLoopAsync().Forget(ex =>
                {
                    if (!IsExpectedDisconnectException(ex))
                    {
                        Debug.LogException(ex);
                    }
                });

                // keep-alive 핑 루프 — 무이동 상태에서도 주기적으로 C_Ping 을 보내 서버 유휴 타임아웃을 방지.
                RunHeartbeatLoopAsync().Forget();
            }
            catch (OperationCanceledException)
            {
                await CleanupConnectionAsync(CancellationToken.None);
                State = SocketSessionState.Disconnected;
                throw;
            }
            catch (Exception e)
            {
                State = SocketSessionState.Failed;
                await CleanupConnectionAsync(CancellationToken.None);
                Debug.LogError(e);
                throw;
            }
        }

        private async UniTask RunReceiveLoopAsync()
        {
            try
            {
                await _connector.StartReceiveLoopAsync(HandlePacketAsync, _sessionCts.Token);

                // 실패나 명시적 종료가 아니라면 원격 종료로 판단해 Disconnected 처리한다.
                if (State != SocketSessionState.Failed &&
                    State != SocketSessionState.Disconnected)
                {
                    State = SocketSessionState.Disconnected;
                }
            }
            // 취소는 정상적인 세션 종료 흐름으로 본다.
            catch (OperationCanceledException)
            {
                if (State != SocketSessionState.Failed)
                {
                    State = SocketSessionState.Disconnected;
                }
            }
            catch (Exception e)
            {
                State = SocketSessionState.Failed;
                Debug.LogError(e);
            }
            finally
            {
                await _connector.DisconnectAsync(CancellationToken.None);
                CancelAndDisposeSessionToken();

                // 의도적 종료(퇴장)가 아니면 = 비정상 끊김 → 메인 스레드에서 1회 통지.
                if (!_intentionalDisconnect)
                {
                    await UniTask.SwitchToMainThread();
                    OnDisconnected?.Invoke();
                }
            }
        }

        /// <summary>
        /// keep-alive 핑 루프 — Connected~Joined 동안 HeartbeatInterval 마다 C_Ping 송신.
        /// 세션 토큰 취소(끊김/퇴장) 시 종료. 송신 실패는 무시(수신 루프가 끊김을 처리).
        /// </summary>
        private async UniTaskVoid RunHeartbeatLoopAsync()
        {
            try
            {
                while (true)
                {
                    await UniTask.Delay(HeartbeatInterval, ignoreTimeScale: true, cancellationToken: _sessionCts.Token);

                    if (State == SocketSessionState.Connected ||
                        State == SocketSessionState.Joining ||
                        State == SocketSessionState.Joined)
                    {
                        await _connector.SendAsync(new C_Ping { IsHealthy = true }, _sessionCts.Token);
                    }
                }
            }
            catch (OperationCanceledException) { /* 세션 종료 — 정상 */ }
            catch (Exception) { /* 끊김 중 송신 실패 — 수신 루프가 끊김을 처리하므로 무시 */ }
        }

        /// <summary>세션 상태에 먼저 반영한 뒤 핸들러에 전달한다(순서 의존).</summary>
        private async UniTask HandlePacketAsync(Packet packet)
        {
            UpdateStateFromPacket(packet);
            await _dispatcher.DispatchAsync(packet);
        }

        private void UpdateStateFromPacket(Packet packet)
        {
            if (packet is S_PlayerJoined joined)
            {
                // 거절 사유를 남긴다 — 버리면 재시도 로그만 쌓이고 원인을 알 수 없다.
                LastJoinFailureReason = joined.Success ? null : joined.Message;
                State = joined.Success
                    ? SocketSessionState.Joined
                    : SocketSessionState.Failed;
            }
        }

        private CancellationTokenSource CreateLinkedToken(CancellationToken ct)
        {
            if (_sessionCts == null)
            {
                throw new InvalidOperationException("SocketSession is not active.");
            }

            return CancellationTokenSource.CreateLinkedTokenSource(_sessionCts.Token, ct);
        }

        /// <summary>
        /// 연결 직후 방 참가 요청을 보낸다. UserId와 RoomId를 함께 전송해 서버에서 Redis로 검증한다.
        /// </summary>
        public async UniTask JoinRoomAsync(CancellationToken ct)
        {
            if (State != SocketSessionState.Connected)
            {
                throw new InvalidOperationException("SocketSession is not connected.");
            }

            State = SocketSessionState.Joining;

            var packet = new C_PlayerJoin
            {
                RoomId = _connectionInfo.RoomId,
                UserId = _connectionInfo.UserId
            };

            using var linkedCts = CreateLinkedToken(ct);
            await _connector.SendAsync(packet, linkedCts.Token);
        }

        /// <summary>Joined 상태에서만 C_PlayerLeave 를 보낸다. DisconnectAsync 전에 호출한다.</summary>
        public async UniTask LeaveRoomAsync(CancellationToken ct)
        {
            if (State != SocketSessionState.Joined)
            {
                Debug.Log($"[SocketSession] LeaveRoomAsync 스킵 — 현재 상태: {State}");
                return;
            }

            using var linkedCts = CreateLinkedToken(ct);
            await _connector.SendAsync(new C_PlayerLeave(), linkedCts.Token);
            Debug.Log("[SocketSession] C_PlayerLeave 전송 완료");
        }

        /// <summary>Joined 상태에서만 이동 패킷을 전송한다(아니면 throw).</summary>
        public async UniTask SendMoveAsync(C_Move packet, CancellationToken ct)
        {
            if (State != SocketSessionState.Joined)
            {
                throw new InvalidOperationException("SocketSession is not joined.");
            }

            using var linkedCts = CreateLinkedToken(ct);
            await _connector.SendAsync(packet, linkedCts.Token);
        }

        public async UniTask SendAsync(Packet packet, CancellationToken ct)
        {
            if (State != SocketSessionState.Joined)
            {
                throw new InvalidOperationException("SocketSession is not joined.");
            }

            using var linkedCts = CreateLinkedToken(ct);
            await _connector.SendAsync(packet, linkedCts.Token);
        }

        public async UniTask DisconnectAsync(CancellationToken ct)
        {
            _intentionalDisconnect = true; // 의도적 종료 — 수신 루프 종료가 OnDisconnected 를 발화하지 않게 한다.

            if (State == SocketSessionState.Disconnected)
            {
                return;
            }

            await CleanupConnectionAsync(ct);
            State = SocketSessionState.Disconnected;
        }

        private async UniTask CleanupConnectionAsync(CancellationToken ct)
        {
            CancelSessionToken();
            await _connector.DisconnectAsync(ct);
            CancelAndDisposeSessionToken();
        }

        private void CancelSessionToken()
        {
            _sessionCts?.Cancel();
        }

        private void CancelAndDisposeSessionToken()
        {
            _sessionCts?.Cancel();
            _sessionCts?.Dispose();
            _sessionCts = null;
        }

        private static bool IsExpectedDisconnectException(Exception exception)
        {
            if (exception is OperationCanceledException)
            {
                return true;
            }

            if (exception is ObjectDisposedException)
            {
                return true;
            }

            if (exception is IOException ioException && ioException.InnerException is SocketException)
            {
                return true;
            }

            return exception is SocketException;
        }
    }
}
