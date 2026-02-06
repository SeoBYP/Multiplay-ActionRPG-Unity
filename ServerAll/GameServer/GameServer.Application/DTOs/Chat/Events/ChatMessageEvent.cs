using MemoryPack;

namespace GameServer.Application.DTOs.Chat.Events;

[MemoryPackable]
public partial class ChatMessageEvent
{
    public ChatType Type { get; set; }
    
    public long? RoomId { get; set; }          // Room 타입일 때만 사용
    public long? TargetUserId { get; set; }    // Whisper 타입일 때만 사용
    public string? TargetUserName { get; set; } // Whisper 타입일 때만 사용
    
    public long SenderUserId { get; set; } // 발신자 ID
    public string SenderUserName { get; set; } // 발신자 Name
    public string Message { get; set; } // 메시지 내용
    public DateTime SentAt { get; set; }  // 귓
    
    public ChatMessageEvent(
        ChatType type,
        long senderUserId, 
        string senderUserName,
        string message, 
        DateTime sentAt,
        long? roomId = null,
        long? targetUserId = null,
        string? targetUserName = null)
    {
        Type = type;
        SenderUserId = senderUserId;
        SenderUserName = senderUserName;
        Message = message;
        SentAt = sentAt;
        RoomId = roomId;
        TargetUserId = targetUserId;
        TargetUserName = targetUserName;
    }
}