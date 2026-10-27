using GameServer.Domain.Entities.Chat;
using GameServer.Grpc.Chat;

namespace GameServer.API.Extension;

public static class ChatMessageInfoExtension
{
    public static ChatMessageInfo ToGrpc(this ChatMessage msg)
    {
        // proto 에 단위 명시가 없어 ms 로 보낸다 — 클라도 ms 로 읽어야 한다.
        var sentAtUnixMs = new DateTimeOffset(msg.SentAt, TimeSpan.Zero).ToUnixTimeMilliseconds();

        return new ChatMessageInfo
        {
            MessageId = msg.MessageId,
            ChatType = msg.ChatType.ToGrpc(),
            SenderNickname = msg.SenderUserNickName,
            Message = msg.Message,
            SentAt = sentAtUnixMs,
            RoomId = msg.RoomId ?? 0,
            TargetUserNickname = msg.TargetUserNickName ?? ""
        };
    }
    
    public static Domain.Entities.Chat.ChatType ToDomain(this Grpc.Chat.ChatType grpcType) => grpcType switch {
        Grpc.Chat.ChatType.Global  => Domain.Entities.Chat.ChatType.Global,
        Grpc.Chat.ChatType.Room    => Domain.Entities.Chat.ChatType.Room,
        Grpc.Chat.ChatType.Whisper => Domain.Entities.Chat.ChatType.Whisper,
        _ => throw new ArgumentException()
    };
    
    public static Grpc.Chat.ChatType ToGrpc(this Domain.Entities.Chat.ChatType domainType) => domainType switch
    {
        Domain.Entities.Chat.ChatType.Global  => Grpc.Chat.ChatType.Global,
        Domain.Entities.Chat.ChatType.Room    => Grpc.Chat.ChatType.Room,
        Domain.Entities.Chat.ChatType.Whisper => Grpc.Chat.ChatType.Whisper,
        _ => throw new ArgumentException()
    };
}