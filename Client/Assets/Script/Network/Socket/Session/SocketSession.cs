using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Network.Socket.Packets;
using UnityEngine;

namespace Game.Network.Socket
{
    public class SocketSession : ISocketSession
    {
        private readonly ISocketConnector _connector;
        private readonly ISocketPacketDispatcher _dispatcher;
        
        private SocketConnectionInfo _connectionInfo;
        private CancellationTokenSource _sessionCts;

        public SocketSessionState State { get; private set; } = SocketSessionState.Idle;

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
                State != SocketSessionState.Disconnected)
            {
                throw new InvalidOperationException("SocketSession is already active.");
            }
            
            try
            {
                _connectionInfo = connectionInfo;
                CancelAndDisposeSessionToken();
                _sessionCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

                State = SocketSessionState.Connecting;

                await _connector.ConnectAsync(connectionInfo.Host, connectionInfo.Port, _sessionCts.Token);

                State = SocketSessionState.Connected;

                RunReceiveLoopAsync().Forget(ex =>
                {
                    if (!IsExpectedDisconnectException(ex))
                    {
                        Debug.LogException(ex);
                    }
                });
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

                if (State != SocketSessionState.Failed &&
                    State != SocketSessionState.Disconnected)
                {
                    State = SocketSessionState.Disconnected;
                }
            }
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
            }
        }


        private async UniTask HandlePacketAsync(Packet packet)
        {
            UpdateStateFromPacket(packet);
            await _dispatcher.DispatchAsync(packet);
        }
        
        private void UpdateStateFromPacket(Packet packet)
        {
            if (packet is S_Auth auth)
            {
                if (auth.Success)
                {
                    State = SocketSessionState.Authenticated;
                }
                else
                {
                    State = SocketSessionState.Failed;
                }

                return;
            }

            if (packet is S_PlayerJoined joined)
            {
                if (joined.Success)
                {
                    State = SocketSessionState.Joined;
                }
                else
                {
                    State = SocketSessionState.Authenticated;
                }

                return;
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
        


        public async UniTask AuthenticateAsync(CancellationToken ct)
        {
            if (State != SocketSessionState.Connected)
            {
                throw new InvalidOperationException("SocketSession is not ready to authenticate.");
            }

            State = SocketSessionState.Authenticating;

            var packet = new C_Auth
            {
                UserId = _connectionInfo.UserId
            };

            using var linkedCts = CreateLinkedToken(ct);
            await _connector.SendAsync(packet, linkedCts.Token);
        }


        public async UniTask JoinRoomAsync(CancellationToken ct)
        {
            if (State != SocketSessionState.Authenticated)
            {
                throw new InvalidOperationException("SocketSession is not authenticated.");
            }

            State = SocketSessionState.Joining;

            var packet = new C_PlayerJoin
            {
                RoomId = _connectionInfo.RoomId
            };

            using var linkedCts = CreateLinkedToken(ct);
            await _connector.SendAsync(packet, linkedCts.Token);
        }

        public async UniTask SendMoveAsync(C_Move packet, CancellationToken ct)
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
