using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Game.Network
{
    
    public static class PacketFactory
    {
        private static readonly Dictionary<PacketType, Func<Packet>> _packetCreators = new();

        public static void RegisterAll()
        {
            _packetCreators.Clear();
            RegisterPacket(PacketType.CHAT, () => new ChatPacket());
            RegisterPacket(PacketType.SET_NICKNAME_S2C, () => new S_SetNicknamePacket());
            // 여기에 다른 PacketType도 추가 가능
        }
        
        private static void RegisterPacket(PacketType type, Func<Packet> creator)
        {
            _packetCreators.Add(type, creator);
        }

        public static Packet Deserialize(PacketType type, byte[] body, int offset = 0)
        {
            if (_packetCreators.TryGetValue(type, out var creator))
            {
                var packet = creator();
                var method = packet.GetType().GetMethod("Deserialize");
                if (method != null)
                {
                    return method.Invoke(packet, new object[] { body, offset }) as Packet;
                }
            }
            UnityEngine.Debug.LogWarning($"알 수 없는 패킷 타입: {type}");
            return null;
        }
        
        private static void Clear()
        {
            _packetCreators.Clear();
        }
    }
    
    
}