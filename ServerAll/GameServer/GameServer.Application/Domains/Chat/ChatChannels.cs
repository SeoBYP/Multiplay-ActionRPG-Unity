

using GameServer.Domain.Entities.Chat;

namespace GameServer.Application.Domains.Chat;

public static class ChatChannels
{
    public const string GlobalChannel = "chat:global";
    public static string RoomChannel(long roomId) => $"chat:room:{roomId}";
    public static string WhisperChannel(string userName) => $"chat:user:{userName}";
    
    // 채널 이름 결정 (chatType에 따라)
    public static string GetChannel(ChatType chatType, long? roomId, string? targetUserNickName)
    {
        return chatType switch
        {
            ChatType.Global => GlobalChannel,
            ChatType.Room => RoomChannel(roomId!.Value),
            ChatType.Whisper => WhisperChannel(targetUserNickName!),
            _ => GlobalChannel
        };
    }
}