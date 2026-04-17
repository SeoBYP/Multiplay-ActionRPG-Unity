using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Network.Socket.Packets;
using UnityEngine;

namespace Game.Network.Socket
{
    public class SocketConnector : ISocketConnector
    {
        public bool IsConnected => _client is { Connected: true } && _stream != null;

        private TcpClient _client;
        private NetworkStream _stream;
        private CancellationTokenSource _receiveCts;
        private SemaphoreSlim _sendLock = new SemaphoreSlim(1);
        
        private const int MaxPacketSize = 4 * 1024 * 1024; // 4MB

        public async UniTask ConnectAsync(string host, int port, CancellationToken ct)
        {
            try
            {
                if(IsConnected)
                    return;
                
                _client = new TcpClient();
                _client.NoDelay = true;
                
                await _client.ConnectAsync(host, port);
                _stream = _client.GetStream();
                _receiveCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                throw;
            }
        }

        public async UniTask SendAsync(Packet packet, CancellationToken ct)
        {
            if(!IsConnected)
                return;
                
            var payload = PacketSerializer.Serialize(packet);
            var lengthBytes = BitConverter.GetBytes(payload.Length);
            
            await _sendLock.WaitAsync(ct);
            
            try
            {
                await _stream.WriteAsync(lengthBytes, 0, 4, ct);
                await _stream.WriteAsync(payload, 0, payload.Length, ct);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                throw;
            }
            finally
            {
                _sendLock.Release();
            }
        }

        public async UniTask StartReceiveLoopAsync(Func<Packet, UniTask> onPacket, CancellationToken ct)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Socket is not connected.");
            }

            try
            {
                while (!ct.IsCancellationRequested && IsConnected)
                {
                    var packet = await ReadPacketAsync(ct);

                    if (packet == null)
                    {
                        break;
                    }

                    await onPacket(packet);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
            }
            catch (ObjectDisposedException) when (ct.IsCancellationRequested || !IsConnected)
            {
            }
            catch (IOException ex) when (IsExpectedDisconnect(ex, ct))
            {
            }
            catch (SocketException ex) when (IsExpectedDisconnect(ex, ct))
            {
            }
        }

        private async UniTask<Packet> ReadPacketAsync(CancellationToken ct)
        {
            var lengthBuffer = new byte[4];
            // Packet 길이를 보장
            var read = await ReadExactlyAsync(lengthBuffer, 4, ct);

            if (read == 0)
            {
                return null;
            }
            
            var payloadLength = BitConverter.ToInt32(lengthBuffer, 0);

            // 패킷의 길이값이 꺠져서 -1 혹은 99999999와 같은 이상한 큰 수를 방지한다.
            if (payloadLength <= 0 || payloadLength > MaxPacketSize)
            {
                throw new InvalidOperationException(
                    $"Invalid payload length: {payloadLength}. Max allowed: {MaxPacketSize}");
            }

            var payload = new byte[payloadLength];
            await ReadExactlyAsync(payload, payloadLength, ct);

            var packet = PacketSerializer.Deserialize(payload);
            
            if (packet == null)
            {
                throw new InvalidOperationException("Failed to deserialize packet.");
            }

            return packet;
        }

        /// <summary>
        /// Stream으로 부터 받은 Bytes들을 명시된 수 만큼 받는 처리를 보장하는 메서드
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="size"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        private async UniTask<int> ReadExactlyAsync(byte[] buffer, int size, CancellationToken ct)
        {
            var offset = 0;

            while (offset < size)
            {
                var read = await _stream.ReadAsync(buffer, offset, size - offset, ct);
                if (read == 0)
                {
                    if (offset == 0)
                    {
                        return 0;
                    }
                    throw new InvalidOperationException("Socket closed during packet read.");
                }
                offset += read;
            }
            return offset;
        }

        private bool IsExpectedDisconnect(Exception exception, CancellationToken ct)
        {
            if (ct.IsCancellationRequested || !IsConnected)
            {
                return true;
            }

            if (exception is IOException ioException && ioException.InnerException is SocketException innerSocketException)
            {
                return IsExpectedSocketError(innerSocketException.SocketErrorCode);
            }

            if (exception is SocketException socketException)
            {
                return IsExpectedSocketError(socketException.SocketErrorCode);
            }

            return false;
        }

        private static bool IsExpectedSocketError(SocketError socketError)
        {
            return socketError == SocketError.OperationAborted
                   || socketError == SocketError.ConnectionAborted
                   || socketError == SocketError.Interrupted
                   || socketError == SocketError.NotConnected
                   || socketError == SocketError.Shutdown;
        }
        
        public UniTask DisconnectAsync(CancellationToken ct)
        {
            _receiveCts?.Cancel();
            _receiveCts?.Dispose();
            _receiveCts = null;

            _stream?.Dispose();
            _stream = null;

            _client?.Close();
            _client = null;

            return UniTask.CompletedTask;
        }
        
        public async ValueTask DisposeAsync()
        {
            await DisconnectAsync(CancellationToken.None);
            _sendLock.Dispose();
        }
    }
}
