using UnityEngine;

namespace Game.Network
{
    public static class PacketHandler
    {
        public static void HandlePacket(Packet packet)
        {
            switch (packet)
            {
                case ChatPacket chat:
                    Debug.Log($"[Chat] {chat.sender} > {chat.message}");
                    break;
                default:
                    Debug.LogWarning("알 수 없는 Packet 처리 시도");
                    break;
            }
        }
    }

}