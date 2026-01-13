using System;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Google.Protobuf;
using ServerCore.Protocol;
using UnityEngine;

namespace Game.Managers
{
    public class NetworkManager : PersistentSingleton<NetworkManager>
    {
        public bool IsConnected => _socket?.Connected ?? false;
        public event Action OnConnected;
        public event Action<string> OnDisconnected;  // reason

        private Socket _socket;
        private CancellationTokenSource _cts;
        private PacketHandler _packetHandler;
        
        protected override void OnInitializeSingleton()
        {
            _packetHandler = new PacketHandler();
            Connect("127.0.0.1", 7777);
        }
        /// <summary>
        /// 서버 연결
        /// </summary>
        public async void Connect(string ip, int port)
        {
            if (IsConnected)
            {
                Debug.LogWarning("[NetworkManager] Already connected");
                return;
            }

            try
            {
                Debug.Log($"[NetworkManager] Connecting to {ip}:{port}...");

                // Socket 생성
                _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _socket.NoDelay = true;

                // 연결
                await _socket.ConnectAsync(ip, port);

                Debug.Log($"[NetworkManager] Connected to {ip}:{port}");

                // CancellationToken 생성
                _cts = new CancellationTokenSource();

                // 수신 루프 시작
                _ = ReceiveLoopAsync(_cts.Token);

                // 연결 이벤트
                OnConnected?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkManager] Connection failed: {e.Message}");
                Disconnect("Connection failed");
            }
        }
        /// <summary>
        /// 연결 종료
        /// </summary>
        public void Disconnect(string reason = "User disconnect")
        {
            if (!IsConnected)
                return;

            Debug.Log($"[NetworkManager] Disconnecting: {reason}");

            try
            {
                _cts?.Cancel();
                _socket?.Shutdown(SocketShutdown.Both);
                _socket?.Close();
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkManager] Disconnect error: {e.Message}");
            }
            finally
            {
                _socket = null;
                _cts?.Dispose();
                _cts = null;

                OnDisconnected?.Invoke(reason);
            }
        }

        /// <summary>
        /// 패킷 전송
        /// 
        /// 구조: [4 bytes Length][Protobuf Data]
        /// </summary>
        public async void SendPacket(Packet packet)
        {
            if (!IsConnected)
            {
                Debug.LogWarning("[NetworkManager] Not connected");
                return;
            }

            try
            {
                // Protobuf 직렬화
                byte[] protobufData = packet.ToByteArray();

                // Length 계산
                int length = protobufData.Length;
                byte[] lengthBytes = BitConverter.GetBytes(length);

                // [Length][Protobuf] 합치기
                byte[] finalPacket = new byte[4 + length];
                Array.Copy(lengthBytes, 0, finalPacket, 0, 4);
                Array.Copy(protobufData, 0, finalPacket, 4, length);

                // 전송
                int offset = 0;
                while (offset < finalPacket.Length)
                {
                    int sent = await _socket.SendAsync(
                        new ArraySegment<byte>(finalPacket, offset, finalPacket.Length - offset),
                        SocketFlags.None);

                    if (sent == 0)
                    {
                        throw new SocketException((int)SocketError.ConnectionReset);
                    }

                    offset += sent;
                }

                Debug.Log($"[NetworkManager] Sent: {packet.PayloadCase}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkManager] Send failed: {e.Message}");
                Disconnect("Send failed");
            }
        }
        
        /// <summary>
        /// 수신 루프 (백그라운드 스레드)
        /// </summary>
        private async Task ReceiveLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested && IsConnected)
                {
                    // Length 읽기 (4 bytes)
                    byte[] lengthBytes = await ReceiveExactAsync(4, ct);
                    int length = BitConverter.ToInt32(lengthBytes, 0);

                    // 비정상 패킷 차단
                    if (length <= 0 || length > 65536)
                    {
                        Debug.LogError($"[NetworkManager] Invalid packet length: {length}");
                        break;
                    }

                    // Protobuf 데이터 읽기
                    byte[] protobufData = await ReceiveExactAsync(length, ct);

                    // Packet 파싱
                    var packet = Packet.Parser.ParseFrom(protobufData);

                    // ⚠️ 중요: Unity 메인 스레드로 전달
                    _packetHandler.HandlePacket(packet);
                }
            }
            catch (OperationCanceledException)
            {
                // 정상 종료
                Debug.Log("[NetworkManager] Receive loop cancelled");
            }
            catch (SocketException)
            {
                // 연결 종료
                Disconnect("Connection lost");
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkManager] Receive error: {e.Message}");
                Disconnect("Receive error");
            }
        }
        
        /// <summary>
        /// 정확히 count만큼 바이트 수신
        /// </summary>
        private async Task<byte[]> ReceiveExactAsync(int count, CancellationToken ct)
        {
            byte[] buffer = new byte[count];
            int offset = 0;

            while (offset < count)
            {
                int received = await _socket.ReceiveAsync(
                    new ArraySegment<byte>(buffer, offset, count - offset),
                    SocketFlags.None,
                    ct);

                if (received == 0)
                {
                    throw new SocketException((int)SocketError.ConnectionReset);
                }

                offset += received;
            }

            return buffer;
        }


        
        private void OnDestroy()
        {
            Disconnect("Application quit");
        }

        private void OnApplicationQuit()
        {
            Disconnect("Application quit");
        }
    }
}