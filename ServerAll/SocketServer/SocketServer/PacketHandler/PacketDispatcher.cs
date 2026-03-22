namespace Server.PacketHandler;

using Microsoft.Extensions.Logging;
using Shared.Packet.Packets;

public sealed class PacketDispatcher
{
    private readonly IReadOnlyDictionary<Type, PacketHandler> _map;
    private readonly ILogger<PacketDispatcher> _logger;

    public PacketDispatcher(IReadOnlyDictionary<Type, PacketHandler> map, ILogger<PacketDispatcher> logger)
    {
        _map = map;
        _logger = logger;
    }

    public ValueTask Dispatch(Session session, Packet packet, CancellationToken ct)
    {
        var packetType = packet.GetType();

        if (_map.TryGetValue(packetType, out var handler))
            return handler(session, packet, ct);

        _logger.LogWarning("No packet handler for {PacketType}", packetType.Name);
        return ValueTask.CompletedTask;
    }
}
