using ServerCore.Protocol;

namespace Server.Packet.Handler;

public sealed class ChatHandlers
{
    [PacketHandler("CChat")]
    public static ValueTask HandleChat(Session session, ServerCore.Protocol.Packet packet, CancellationToken ct)
    {
        var request = packet.CChat;
        
        // 닉네임 설정 확인
        if (string.IsNullOrEmpty(session.Nickname))
        {
            var errorPacket = new S_Chat
            {
                ChatType = ChatType.All,
                SenderNickname = "SYSTEM",
                Message = "Please set your nickname first"
            };
            return SendSChat(session, errorPacket, ct);
        }
        
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return ValueTask.CompletedTask;
        }
        
        // 메시지 길이 제한
        if (request.Message.Length > 500)
        {
            request.Message = request.Message.Substring(0, 500);
        }

        var chatPacket = new S_Chat
        {
            ChatType = request.ChatType,
            SenderId = session.SessionId,
            SenderNickname = session.Nickname,
            Message = request.Message
        };

        switch (request.ChatType)
        {
            case ChatType.All:
                HandleAllChat(session, chatPacket, ct);
                break;
                    
            case ChatType.Whisper:
                HandleWhisper(session, chatPacket, request.TargetNickname, ct);
                break;
                    
            case ChatType.Room:
                HandleRoomChat(session, chatPacket,ct);
                break;
        }
        
        return ValueTask.CompletedTask;
    }

    private static void HandleAllChat(Session session, S_Chat chatPacket, CancellationToken ct)
    {
        var responsePacket = new ServerCore.Protocol.Packet
        {
            SChat = chatPacket
        };
        SessionManager.Instance?.BroadcastAll(responsePacket, ct);
        Console.WriteLine($"[ALL] {session.Nickname}: {chatPacket.Message}");
    }

    private static void HandleWhisper(Session sender, S_Chat chatPacket, string targetNickname,
        CancellationToken ct)
    {
        chatPacket.TargetNickname = targetNickname;
        
        var targetSession = SessionManager.Instance?.GetWithNickname(targetNickname);

        if (targetSession is null)
        {
            var errorPacket = new S_Chat
            {
                ChatType = ChatType.Whisper,
                SenderNickname = "SYSTEM",
                Message = "Target user not found"
            };
            SendSChat(sender, errorPacket, ct);
            return;
        }
        
        SendSChat(targetSession, chatPacket, ct);
        SendSChat(sender, chatPacket, ct);
        Console.WriteLine($"[WHISPER] {sender.Nickname} → {targetSession.Nickname}: {chatPacket.Message}");
    }

    private static void HandleRoomChat(Session sender, S_Chat chatPacket, CancellationToken ct)
    {
        // var currentRoom = sender.GetPlayerRoom();
        // if (currentRoom is null)
        // {
        //     var errorPacket = new S_Chat
        //     {
        //         ChatType = ChatType.Room,
        //         SenderNickname = "SYSTEM",
        //         Message = "You are not in a room"
        //     };
        //     SendSChat(sender, errorPacket, ct);
        //     return;
        // }
        //
        // var responsePacket = new ServerCore.Protocol.Packet
        // {
        //     SChat = chatPacket
        // };
        // currentRoom.Broadcast(responsePacket);
        //
        // Console.WriteLine($"[ROOM {currentRoom.RoomId}] {sender.Nickname}: {chatPacket.Message}");
    }

    private static ValueTask SendSChat(Session session, S_Chat chatPacket, CancellationToken ct)
    {
        var responsePacket = new ServerCore.Protocol.Packet
        {
            SChat = chatPacket
        };
        _ = session.SendPacketAsync(responsePacket, ct);
        return ValueTask.CompletedTask;
    }
}