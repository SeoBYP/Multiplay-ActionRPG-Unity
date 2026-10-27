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
        private const int MaxPacketSize = 4 * 1024 * 1024;

        private TcpClient _client;
        private NetworkStream _stream;
        private CancellationTokenSource _receiveCts;
        private SemaphoreSlim _sendLock = new SemaphoreSlim(1);

        public bool IsConnected => _client is { Connected: true } && _stream != null;

        public async UniTask ConnectAsync(string host, int port, CancellationToken ct)
        {
            try
            {
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

        public async UniTask SendAsync(Packet packet, CancellationToken ct)
        {
            // 연결이 없으면 조용히 무시한다.
            if (!IsConnected)
                return;

            var payload = PacketSerializer.Serialize(packet);
            var lengthBytes = BitConverter.GetBytes(payload.Length);

            // 여러 송신 요청이 동시에 들어와도 프레임 경계가 섞이지 않도록 직렬화한다.
            await _sendLock.WaitAsync(ct);

            try
            {
                // 락 대기 중 DisconnectAsync 가 _stream 을 null 로 만들 수 있다(송신↔종료 레이스) — 로컬로 캡처해 방어.
                var stream = _stream;
                if (stream == null)
                    return; // 끊김은 정상 경로

                await stream.WriteAsync(lengthBytes, 0, 4, ct);
                await stream.WriteAsync(payload, 0, payload.Length, ct);
            }
            catch (Exception e) when (IsExpectedDisconnect(e, ct))
            {
                // 예상 종료 — 로그·throw 안 함.
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

        private async UniTask<Packet> ReadPacketAsync(CancellationToken ct)
        {
            var lengthBuffer = new byte[4];
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

        /// <summary>정확히 size 바이트를 읽는다. 중간 EOF 는 불완전한 패킷으로 간주한다.</summary>
        private async UniTask<int> ReadExactlyAsync(byte[] buffer, int size, CancellationToken ct)
        {
            var offset = 0;

            while (offset < size)
            {
                ct.ThrowIfCancellationRequested();

                var stream = _stream;
                if (stream == null)
                    throw new ObjectDisposedException(nameof(SocketConnector));

                var read = await stream.ReadAsync(buffer, offset, size - offset, ct);
                if (read == 0)
                {
                    if (offset == 0)
                        return 0;

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
