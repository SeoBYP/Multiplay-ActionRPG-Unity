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

        /// <summary>
        /// 서버에 연결하고 수신 루프를 시작한다.
        /// </summary>
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
                // 이전 세션 취소 토큰이 남아 있으면 정리 후 새 세션 토큰을 만든다.
                CancelAndDisposeSessionToken();
                _sessionCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

                State = SocketSessionState.Connecting;
                _intentionalDisconnect = false; // 새 세션 — 비정상 끊김 발화 가능 상태로 리셋

                await _connector.ConnectAsync(connectionInfo.Host, connectionInfo.Port, _sessionCts.Token);

                State = SocketSessionState.Connected;

                // 백그라운드 수신 루프는 세션 수명 동안 계속 패킷을 처리한다.
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

        /// <summary>
        /// 패킷 수신 루프를 실행하고 종료 원인에 따라 세션 상태를 정리한다.
        /// </summary>
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
            // 그 외 예외는 세션 실패로 전환한다.
            catch (Exception e)
            {
                State = SocketSessionState.Failed;
                Debug.LogError(e);
            }
            finally
            {
                // 수신 루프 종료 시 커넥터와 세션 토큰을 모두 정리한다.
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

        /// <summary>
        /// 수신된 패킷을 세션 상태에 반영한 뒤 등록된 핸들러에 전달한다.
        /// </summary>
        private async UniTask HandlePacketAsync(Packet packet)
        {
            UpdateStateFromPacket(packet);
            await _dispatcher.DispatchAsync(packet);
        }

        /// <summary>
        /// 특정 서버 패킷에 따라 세션 상태 머신을 전이시킨다.
        /// </summary>
        private void UpdateStateFromPacket(Packet packet)
        {
            if (packet is S_PlayerJoined joined)
            {
                // 거절 사유를 남긴다 — 버리면 재시도 로그만 쌓이고 원인을 알 수 없다(실측: "Room is full" 을 못 봤다).
                LastJoinFailureReason = joined.Success ? null : joined.Message;
                State = joined.Success
                    ? SocketSessionState.Joined
                    : SocketSessionState.Failed;
            }
        }

        /// <summary>
        /// 세션 수명 토큰과 외부 토큰을 묶은 linked token을 만든다.
        /// </summary>
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

        /// <summary>
        /// 방 퇴장 패킷을 전송한다. Joined 상태에서만 유효하며, DisconnectAsync 전에 호출한다.
        /// </summary>
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

        /// <summary>
        /// Joined 상태에서만 이동 패킷을 전송한다.
        /// </summary>
        public async UniTask SendMoveAsync(C_Move packet, CancellationToken ct)
        {
            if (State != SocketSessionState.Joined)
            {
                throw new InvalidOperationException("SocketSession is not joined.");
            }

            using var linkedCts = CreateLinkedToken(ct);
            await _connector.SendAsync(packet, linkedCts.Token);
        }

        /// <summary>
        /// 임의 패킷을 Joined 상태에서 전송한다. (C_Attack 등 게임플레이 패킷)
        /// </summary>
        public async UniTask SendAsync(Packet packet, CancellationToken ct)
        {
            if (State != SocketSessionState.Joined)
            {
                throw new InvalidOperationException("SocketSession is not joined.");
            }

            using var linkedCts = CreateLinkedToken(ct);
            await _connector.SendAsync(packet, linkedCts.Token);
        }

        /// <summary>
        /// 세션을 종료하고 연결 리소스를 정리한다.
        /// </summary>
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

        /// <summary>
        /// 현재 연결에 연결된 토큰과 네트워크 리소스를 일괄 정리한다.
        /// </summary>
        private async UniTask CleanupConnectionAsync(CancellationToken ct)
        {
            CancelSessionToken();
            await _connector.DisconnectAsync(ct);
            CancelAndDisposeSessionToken();
        }

        /// <summary>
        /// 세션 전용 취소 토큰에 취소를 전달한다.
        /// </summary>
        private void CancelSessionToken()
        {
            _sessionCts?.Cancel();
        }

        /// <summary>
        /// 세션 토큰을 취소 후 해제하고 null로 비운다.
        /// </summary>
        private void CancelAndDisposeSessionToken()
        {
            _sessionCts?.Cancel();
            _sessionCts?.Dispose();
            _sessionCts = null;
        }

        /// <summary>
        /// 연결 종료 과정에서 예상 가능한 예외인지 판별한다.
        /// </summary>
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
