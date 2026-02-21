using GameServer.Grpc.Chat;

namespace GameServer.API.Extension;

public static class ChatMessageInfoExtension
{
    public static ChatMessageInfo ToGrpc(this Domain.Entities.Chat.ChatMessage chatMessage) => new ChatMessageInfo
    {
        ChatType = chatMessage.ChatType.ToGrpc(),
        SenderUserId = chatMessage.SenderUserId,
        SenderUserName = chatMessage.SenderUserName,
        Message = chatMessage.Message,
        SentAt = new DateTimeOffset(chatMessage.SentAt).ToUnixTimeSeconds(),
        RoomId = chatMessage.RoomId ?? 0,
        TargetUserId = chatMessage.TargetUserId ?? 0,
    };
}