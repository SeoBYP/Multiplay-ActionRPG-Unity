using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Network
{
    public static class PacketHandler
    {
        private static readonly Dictionary<Type, IPacketHandler> _handlers = new();

        public static void Register<T>(IPacketHandler handler) where T : Packet
        {
            _handlers[typeof(T)] = handler;
        }

        public static IPacketHandler GetHandler<T>() where T : Packet
        {
            return _handlers.GetValueOrDefault(typeof(T));
        }
        
        public static void HandlePacket(Packet packet)
        {
            var type = packet.GetType();
            if (_handlers.TryGetValue(type, out var handler))
            {
                handler.Handle(packet);
            }
            else
            {
                Debug.LogWarning($"[PacketHandler] 등록되지 않은 패킷 타입: {type}");
            }
        }
    }
}