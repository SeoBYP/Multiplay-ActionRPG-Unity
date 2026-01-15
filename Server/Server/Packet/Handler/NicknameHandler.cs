using System.Collections.Concurrent;
using ServerCore.Protocol;

namespace Server.Packet.Handler;

public class NicknameHandler
{
    private static ConcurrentDictionary<string, ulong> _nicknameToSessionId = new();
    
    [PacketHandler("CSetNickname")]
    public static ValueTask HandlerCNickname(Session session, ServerCore.Protocol.Packet packet, CancellationToken ct)
    {
        var request = packet.CSetNickname;
        
        var response = new S_SetNickname();

        // 닉네임이 비어 있는지
        if (string.IsNullOrWhiteSpace(request.Nickname))
        {
            response.Success = false;
            response.ErrorMessage = "Nickname cannot be empty";
            return SendSNickname(session, response, ct);
        }

        if (request.Nickname.Length < 2 || request.Nickname.Length > 20)
        {
            response.Success = false;
            response.ErrorMessage = "Nickname must be 2-20 characters";
            return SendSNickname(session, response, ct);
        }

        if (_nicknameToSessionId.ContainsKey(request.Nickname))
        {
            response.Success = false;
            response.ErrorMessage = "Nickname already in use";
            return SendSNickname(session, response, ct);
        }
        
        if (!string.IsNullOrEmpty(session.Nickname))
        {
            _nicknameToSessionId.TryRemove(session.Nickname, out _);
        }
        
        session.Nickname = request.Nickname;
        _nicknameToSessionId[request.Nickname] = session.SessionId;
        Console.WriteLine($"[Nickname] Session {session.SessionId} set nickname to '{request.Nickname}'");

        response.Success = true;
        response.Nickname = session.Nickname;
        return SendSNickname(session, response, ct);
    }

    private static ValueTask SendSNickname(Session session, S_SetNickname response, CancellationToken ct)
    {
        var responsePacket = new ServerCore.Protocol.Packet
        {
            SSetNickname = response
        };
        _ = session.SendPacketAsync(responsePacket, ct);
        
        return ValueTask.CompletedTask;
    }
}