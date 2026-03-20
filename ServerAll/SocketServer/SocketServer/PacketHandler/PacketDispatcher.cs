namespace Server.PacketHandler;

using Shared.Packet.Packets;

public sealed class PacketDispatcher
{
    private readonly IReadOnlyDictionary<Type, PacketHandler> _map;

    public PacketDispatcher(IReadOnlyDictionary<Type, PacketHandler> map)
    {
        _map = map;
    }

    public ValueTask Dispatch(Session session, Packet packet, CancellationToken ct)
    {
        var packetType = packet.GetType();

        if (_map.TryGetValue(packetType, out var handler))
            return handler(session, packet, ct);

        Console.WriteLine($"[PacketDispatcher] No handler for {packetType.Name}");
        return ValueTask.CompletedTask;
    }
}