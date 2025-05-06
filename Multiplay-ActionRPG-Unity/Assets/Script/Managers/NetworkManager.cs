using System;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Cysharp.Threading.Tasks;
using Game.Network;
using R3;
using Unity.VisualScripting;
using UnityEngine;

namespace Game.Managers
{
    public class NetworkManager : PersistentSingleton<NetworkManager>
    {
        private TcpClient _client;
        private NetworkStream _stream;

        private byte[] _recvBuffer = new byte[4096];

        public Action OnConnected;
        
        protected override void OnInitializeSingleton()
        {
            PacketFactory.RegisterAll();
            RegisterPacketHandler();
            _ = Connect("127.0.0.1", 4242);
        }

        private void RegisterPacketHandler()
        {
            PacketHandler.Register<ChatPacket>(new ChatPacketHandler());
            PacketHandler.Register<S_SetNicknamePacket>(new SetNicknamePacketHandler());
        }

        private async UniTask Connect(string host, int port)
        {
            Debug.Log("Start Connect");
            _client = new TcpClient();
            await _client.ConnectAsync(host, port);
            _stream = _client.GetStream();
            OnConnected?.Invoke();
            Debug.Log("서버 연결 성공");

            _ = ReceiveLoop(); // ✅ 여기서만 실행
        }


        public async UniTask SendPacket(PacketType packetType, byte[] body)
        {
            PacketHeader header = new PacketHeader
            {
                type = packetType,
                size = (uint)body.Length
            };
            byte[] headerBytes = PacketHeader.Serialize(header);
            byte[] packet = new byte[headerBytes.Length + body.Length];
            Buffer.BlockCopy(headerBytes, 0, packet, 0, headerBytes.Length);
            Buffer.BlockCopy(body, 0, packet, headerBytes.Length, body.Length);

            await _stream.WriteAsync(packet, 0, packet.Length);
        }

        public async UniTask SendPacket(Packet packet)
        {
            byte[] body = packet.Serialize();
            await SendPacket(packet.PacketType, body);
        }

        private async UniTask ReceiveLoop()
        {
            try
            {
                while (_client != null && _client.Connected)
                {
                    byte[] buffer = new byte[4096]; // 새로 버퍼 생성
                    int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);

                    if (bytesRead > 0)
                    {
                        ProcessReceived(buffer);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ReceiveLoop 오류] {ex.Message}");
            }
        }

        private void ProcessReceived(byte[] buffer)
        {
            PacketHeader header = PacketHeader.Deserialize(buffer);
            int offset = Marshal.SizeOf(typeof(PacketHeader));
            Packet pkt = PacketFactory.Deserialize(header.type, buffer, offset);

            if (pkt != null)
            {
                PacketHandler.HandlePacket(pkt);
            }
        }
    }
}