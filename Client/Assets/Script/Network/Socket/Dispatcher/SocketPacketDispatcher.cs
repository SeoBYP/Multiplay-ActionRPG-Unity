using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Network.Socket.Packets;

namespace Game.Network.Socket
{
    /// <summary>
    /// 패킷 타입을 기준으로 등록된 핸들러를 찾아 실행하는 디스패처.
    /// </summary>
    public class SocketPacketDispatcher : ISocketPacketDispatcher
    {
        private readonly Dictionary<Type, IPacketHandler> _handlers;

        public SocketPacketDispatcher(IEnumerable<IPacketHandler> handlers)
        {
            _handlers = new Dictionary<Type, IPacketHandler>();

            foreach (var handler in handlers)
            {
                // 같은 패킷 타입에 여러 핸들러가 등록되면 처리 순서를 보장할 수 없으므로 막는다.
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

            // 등록된 핸들러가 있으면 위임하고, 없으면 조용히 무시한다.
            if (_handlers.TryGetValue(packet.GetType(), out var handler))
            {
                return handler.HandleAsync(packet);
            }

            return UniTask.CompletedTask;
        }
    }
}
