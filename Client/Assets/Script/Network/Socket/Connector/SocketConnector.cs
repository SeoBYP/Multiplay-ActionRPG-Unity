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
    /// <summary>
    /// TCP 소켓 연결과 길이 기반 패킷 프레이밍을 담당하는 저수준 커넥터.
    /// </summary>
    public class SocketConnector : ISocketConnector
    {
        private const int MaxPacketSize = 4 * 1024 * 1024; // 4MB

        private TcpClient _client;
        private NetworkStream _stream;
        private CancellationTokenSource _receiveCts;
        private SemaphoreSlim _sendLock = new SemaphoreSlim(1);

        public bool IsConnected => _client is { Connected: true } && _stream != null;

        /// <summary>
        /// 서버와 TCP 연결을 만들고 네트워크 스트림을 준비한다.
        /// </summary>
        public async UniTask ConnectAsync(string host, int port, CancellationToken ct)
        {
            try
            {
                // 이미 연결되어 있으면 재연결하지 않는다.
                if (IsConnected)
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

        /// <summary>
        /// 패킷을 직렬화해 길이 프리픽스와 함께 전송한다.
        /// </summary>
        public async UniTask SendAsync(Packet packet, CancellationToken ct)
        {
            // 연결이 없으면 호출은 무시한다.
            if (!IsConnected)
                return;

            var payload = PacketSerializer.Serialize(packet);
            var lengthBytes = BitConverter.GetBytes(payload.Length);

            // 여러 송신 요청이 동시에 들어와도 프레임 경계가 섞이지 않도록 직렬화한다.
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

        /// <summary>
        /// 연결이 유지되는 동안 패킷을 계속 읽어 상위 콜백으로 전달한다.
        /// </summary>
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

                    // EOF면 상대가 연결을 종료한 것으로 보고 루프를 종료한다.
                    if (packet == null)
                    {
                        break;
                    }

                    await onPacket(packet);
                }
            }
            // 세션 종료에 의해 취소된 경우는 정상 종료로 본다.
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
            }
            catch (ObjectDisposedException) when (ct.IsCancellationRequested || !IsConnected)
            {
            }
            // 네트워크 끊김 계열 예외는 예상 가능한 종료로 간주한다.
            catch (IOException ex) when (IsExpectedDisconnect(ex, ct))
            {
            }
            catch (SocketException ex) when (IsExpectedDisconnect(ex, ct))
            {
            }
        }

        /// <summary>
        /// 길이 프리픽스 기반으로 패킷 하나를 읽고 역직렬화한다.
        /// </summary>
        private async UniTask<Packet> ReadPacketAsync(CancellationToken ct)
        {
            var lengthBuffer = new byte[4];
            // 패킷 길이 4바이트를 먼저 모두 읽는다.
            var read = await ReadExactlyAsync(lengthBuffer, 4, ct);

            if (read == 0)
            {
                return null;
            }

            var payloadLength = BitConverter.ToInt32(lengthBuffer, 0);

            // 비정상적인 길이값으로 인한 메모리 폭주를 막기 위해 상한을 둔다.
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
        /// 네트워크 스트림에서 요청한 바이트 수만큼 정확히 읽어 올 때까지 반복한다.
        /// EOF가 중간에 발생하면 불완전한 패킷으로 간주한다.
        /// </summary>
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

        /// <summary>
        /// 연결 해제 과정에서 흔히 발생하는 예외인지 판별한다.
        /// </summary>
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

        /// <summary>
        /// 정상 종료 시 자주 관찰되는 소켓 에러 코드를 필터링한다.
        /// </summary>
        private static bool IsExpectedSocketError(SocketError socketError)
        {
            return socketError == SocketError.OperationAborted
                   || socketError == SocketError.ConnectionAborted
                   || socketError == SocketError.Interrupted
                   || socketError == SocketError.NotConnected
                   || socketError == SocketError.Shutdown;
        }

        /// <summary>
        /// 수신 루프를 중단하고 네트워크 자원을 해제한다.
        /// </summary>
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

        /// <summary>
        /// 비동기 해제 시 소켓 연결과 송신 락을 모두 정리한다.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            await DisconnectAsync(CancellationToken.None);
            _sendLock.Dispose();
        }
    }
}
