using GameServer.Domain.Entities.Chat;
using GameServer.Grpc.Chat;

namespace GameServer.API.Extension;

public static class ChatMessageInfoExtension
{
    public static ChatMessageInfo ToGrpc(this ChatMessage msg)
    {
        // proto 주석은 "Unix timestamp"라 초/밀리초가 애매함.
        // 여기서는 ms 기준으로 보냄(클라에서 통일 필요). 필요하면 ToUnixTimeSeconds로 변경.
        var sentAtUnixMs = new DateTimeOffset(msg.SentAt, TimeSpan.Zero).ToUnixTimeMilliseconds();

        return new ChatMessageInfo
        {
            MessageId = msg.MessageId,
            ChatType = msg.ChatType switch
            {
                GameServer.Domain.Entities.Chat.ChatType.Global => GameServer.Grpc.Chat.ChatType.Global,
                GameServer.Domain.Entities.Chat.ChatType.Room => GameServer.Grpc.Chat.ChatType.Room,
                GameServer.Domain.Entities.Chat.ChatType.Whisper => GameServer.Grpc.Chat.ChatType.Whisper,
                _ => GameServer.Grpc.Chat.ChatType.Unspecified
            },
            SenderNickname = msg.SenderUserNickName,
            Message = msg.Message,
            SentAt = sentAtUnixMs,
            RoomId = msg.RoomId ?? 0,
            TargetUserNickname = msg.TargetUserNickName ?? ""
        };
    }
}