using ServerCore.Protocol;
using UnityEngine;
using UnityEngine.Events;

namespace Game.Managers
{
    public class PacketHandler
    {
        // Chat
        public UnityEvent<S_Chat> OnChatReceived = new UnityEvent<S_Chat>();

        // Connection
        public UnityEvent OnConnectedEvent = new UnityEvent();
        public UnityEvent<string> OnDisconnectedEvent = new UnityEvent<string>();

        public PacketHandler()
        {
            NetworkManager.Instance.OnConnected += () =>
            {
                Debug.Log("[PacketHandler] Connected");
                OnConnectedEvent?.Invoke();
            };

            NetworkManager.Instance.OnDisconnected += (reason) =>
            {
                Debug.Log($"[PacketHandler] Disconnected: {reason}");
                OnDisconnectedEvent?.Invoke(reason);
            };
        }
        
        /// <summary>
        /// 패킷 처리 (메인 스레드에서 호출됨)
        /// </summary>
        public void HandlePacket(Packet packet)
        {
            switch (packet.PayloadCase)
            {
                case Packet.PayloadOneofCase.SChat:
                    HandleChat(packet.SChat);
                    break;

                
                default:
                    Debug.LogWarning($"[PacketHandler] Unknown packet: {packet.PayloadCase}");
                    break;
            }
        }
        
        private void HandleChat(S_Chat chat)
        {
            Debug.Log($"[PacketHandler] Chat: [{chat.SenderId}] {chat.Message}");
            OnChatReceived?.Invoke(chat);
        }
    }
}