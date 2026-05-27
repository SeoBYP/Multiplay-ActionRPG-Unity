using Serilog;
using Shared.Packet.Packets;

namespace Server.PacketHandler.Handler;

public static class PingPongHandler
{
    [PacketHandler(typeof(C_Ping))]
    public static async ValueTask HandlePing(Session session, C_Ping packet, CancellationToken ct)
    {
        Log.Information("Ping received from UserId={UserId} Healthy={IsHealthy}", session.UserId, packet.IsHealthy);
        await session.SendPacketAsync(new S_Pong { IsHealthy = true }, ct);
    }
}
