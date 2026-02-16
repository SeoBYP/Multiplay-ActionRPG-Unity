namespace GameServer.Domain.Entities.Chat;

public class ChatMessage
{
    public long MessageId { get; private set; }
    public ChatType ChatType { get; private set; }
    public long SenderUserId { get; private set; }
    public string SenderUserName { get; private set; } = "";
    public string Message { get; private set; } = "";
    public DateTime SentAt { get; private set; }

    // Optional fields
    public long? RoomId { get; private set; }
    public long? TargetUserId { get; private set; }

    public ChatMessage()
    {
    }

    public static ChatMessage Create (
        long senderUserId,
        string senderUserName,
        ChatType chatType,
        string message,
        long? roomId = null,
        long? targetUserId = null)
    {
        // 1. SenderUserId 검증
        if (senderUserId <= 0)
            throw new ArgumentException("SenderUserId must be positive", nameof(senderUserId));
        
        // 2. SenderUserName 검증
        if (string.IsNullOrWhiteSpace(senderUserName))
            throw new ArgumentException("SenderUserName cannot be empty", nameof(senderUserName));
        
        // 3. Message 검증
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message cannot be empty", nameof(message));

        // 4. ChatType별 검증
        if (chatType == ChatType.Room && (!roomId.HasValue || roomId <= 0))
            throw new ArgumentException("RoomId is required for room chat", nameof(roomId));
        
        if (chatType == ChatType.Whisper && (!targetUserId.HasValue || targetUserId <= 0))
            throw new ArgumentException("TargetUserId is required for whisper", nameof(targetUserId));

        // 5. 욕설 필터링 (선택)
        var filteredMessage = FilterProfanity(message);
        
        return new ChatMessage
        {
            SenderUserId = senderUserId,
            SenderUserName = senderUserName,
            ChatType = chatType,
            Message = filteredMessage,
            SentAt = DateTime.UtcNow,
            RoomId = roomId,
            TargetUserId = targetUserId,
        };
    }
    
    /// <summary>
    /// Redis에서 복원 (히스토리용)
    /// </summary>
    public static ChatMessage FromRedis(
        long messageId,
        long senderUserId,
        string senderUserName,
        ChatType chatType,
        string message,
        DateTime sentAt,
        long? roomId = null,
        long? targetUserId = null)
    {
        return new ChatMessage
        {
            MessageId = messageId,
            SenderUserId = senderUserId,
            SenderUserName = senderUserName,
            ChatType = chatType,
            Message = message,
            SentAt = sentAt,
            RoomId = roomId,
            TargetUserId = targetUserId,
        };
    }
    
    /// <summary>
    /// 욕설 필터링 (비즈니스 로직)
    /// </summary>
    private static string FilterProfanity(string message)
    {
        // TODO: 실제 욕설 필터 라이브러리 사용
        // 예시: 간단한 치환
        var filtered = message;
        var profanities = new[] { "욕설1", "욕설2", "비속어" };
        
        foreach (var word in profanities)
        {
            if (filtered.Contains(word, StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Replace(word, new string('*', word.Length), 
                    StringComparison.OrdinalIgnoreCase);
            }
        }
        
        return filtered;
    }
    
    /// <summary>
    /// MessageId 설정 (Repository에서만 호출)
    /// </summary>
    public void SetMessageId(long messageId)
    {
        if (MessageId != 0)
            throw new InvalidOperationException("MessageId already set");
        MessageId = messageId;
    }
}