namespace Server.Packet;

public delegate ValueTask PacketHandler(Session session, ServerCore.Protocol.Packet packet, CancellationToken ct);

public sealed class PacketDispatcher(
    IReadOnlyDictionary<ServerCore.Protocol.Packet.PayloadOneofCase, PacketHandler> map)
{
    public ValueTask Dispatch(Session session, ServerCore.Protocol.Packet packet, CancellationToken ct)
    {
        if (map.TryGetValue(packet.PayloadCase, out var handler))
            return handler(session, packet, ct);
        return ValueTask.CompletedTask;
    }
}