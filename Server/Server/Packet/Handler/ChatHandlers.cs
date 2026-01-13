using ServerCore.Protocol;

namespace Server.Packet.Handler;

public sealed class ChatHandlers
{
    [PacketHandler("CChat")]
    public static ValueTask HandleChat(Session session, ServerCore.Protocol.Packet packet, CancellationToken ct)
    {
        var chat = packet.CChat;
        Console.WriteLine($"[Session {session.SessionId}] C_Chat: {chat.Message}");
        
        var sessionRoom = session.GetPlayerRoom();

        if (sessionRoom == null)
        {
            var errorPacket = new ServerCore.Protocol.Packet();
            errorPacket.SChat = new S_Chat()
            {
                SenderId = 0,
                Message = "[System] You are not in any room"
            };
            _ = session.SendPacketAsync(errorPacket, ct);
            return ValueTask.CompletedTask;
        }
        
        var responsePacket = new ServerCore.Protocol.Packet();
        responsePacket.SChat = new S_Chat
        {
            SenderId = session.SessionId,
            Message = chat.Message
        };
        sessionRoom.Broadcast(responsePacket);
        
        return ValueTask.CompletedTask;
    }
}