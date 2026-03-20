namespace Server.PacketHandler;
using Shared.Packet.Packets;

public delegate ValueTask PacketHandler(Session session, Packet packet, CancellationToken ct);