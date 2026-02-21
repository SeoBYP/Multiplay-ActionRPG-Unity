using GameServer.Grpc.Chat;

namespace GameServer.API.Extension;

public static class ChatMessageInfoExtension
{
    public static ChatMessageInfo ToGrpc(this Domain.Entities.Chat.ChatMessage chatMessage) => new ChatMessageInfo
    {
        ChatType = chatMessage.ChatType.ToGrpc(),
        SenderUserName = chatMessage.SenderUserName,
        Message = chatMessage.Message,
        SentAt = new DateTimeOffset(chatMessage.SentAt).ToUnixTimeSeconds(),
        RoomId = chatMessage.RoomId ?? 0,
        TargetUserNickname = chatMessage.TargetUserNickName ?? string.Empty,
    };
}