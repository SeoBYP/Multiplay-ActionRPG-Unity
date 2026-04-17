using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Network.Socket.Packets;

namespace Game.Network.Socket
{
    public class SocketPacketDispatcher : ISocketPacketDispatcher
    {
        private readonly Dictionary<Type, IPacketHandler> _handlers;

        public SocketPacketDispatcher(IEnumerable<IPacketHandler> handlers)
        {
            _handlers = new Dictionary<Type, IPacketHandler>();

            foreach (var handler in handlers)
            {
                if (!_handlers.TryAdd(handler.PacketType, handler))
                {
                    throw new InvalidOperationException($"Duplicate packet handler: {handler.PacketType.Name}");
                }
            }
        }

        public UniTask DispatchAsync(Packet packet, CancellationToken ct = default)
        {
            if (packet == null)
            {
                throw new ArgumentNullException(nameof(packet));
            }

            if (_handlers.TryGetValue(packet.GetType(), out var handler))
            {
                return handler.HandleAsync(packet);
            }

            return UniTask.CompletedTask;
        }
    }
}