namespace GameServer.Domain.Entities.Chat;

public class ChatMessage
{
    public const int MaxMessageLength = 1000;
    
    public long MessageId { get; init; }
    public ChatType ChatType { get; init; }
    public string SenderUserNickName { get; init; } = "";
    public string Message { get; init; } = "";
    public DateTime SentAt { get; init; }

    // Optional fields
    public long? RoomId { get; init; }
    public string? TargetUserNickName { get; init; }

    public ChatMessage() { }

    /// <summary>
    /// 신규 메시지 생성 (ID는 Repository가 먼저 채번해서 전달)
    /// - 검증 + 욕설 필터 + SentAt(UtcNow) 포함
    /// </summary>
    public static ChatMessage CreateNew(
        long messageId,
        string senderUserName,
        ChatType chatType,
        string message,
        long? roomId = null,
        string? targetUserNickName = null)
    {
        Validate(senderUserName, chatType, message, roomId, targetUserNickName);

        return new ChatMessage
        {
            MessageId = messageId,
            SenderUserNickName = senderUserName,
            ChatType = chatType,
            Message = message,
            SentAt = DateTime.UtcNow,
            RoomId = roomId,
            TargetUserNickName = targetUserNickName,
        };
    }

    /// <summary>
    /// Redis에서 복원 (히스토리용) - 저장된 값 그대로 복원
    /// </summary>
    public static ChatMessage FromRedis(
        long messageId,
        string senderUserName,
        ChatType chatType,
        string message,
        DateTime sentAt,
        long? roomId = null,
        string? targetUserNickName = null)
    {
        return new ChatMessage
        {
            MessageId = messageId,
            SenderUserNickName = senderUserName,
            ChatType = chatType,
            Message = message,
            SentAt = sentAt,
            RoomId = roomId,
            TargetUserNickName = targetUserNickName,
        };
    }

    /// <summary>
    /// 검증 로직 (도메인 규칙)
    /// </summary>
    public static void Validate(
        string senderUserName,
        ChatType chatType,
        string message,
        long? roomId,
        string? targetUserNickName)
    {
        if (string.IsNullOrWhiteSpace(senderUserName))
            throw new ArgumentException("SenderUserName cannot be empty", nameof(senderUserName));

        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message cannot be empty", nameof(message));

        if (message.Length > MaxMessageLength)
            throw new ArgumentException($"Message cannot exceed {MaxMessageLength} characters", nameof(message));

        if (chatType == ChatType.Room && (!roomId.HasValue || roomId <= 0))
            throw new ArgumentException("RoomId is required for room chat", nameof(roomId));

        if (chatType == ChatType.Whisper && string.IsNullOrWhiteSpace(targetUserNickName))
            throw new ArgumentException("TargetUserNickName is required for whisper", nameof(targetUserNickName));
    }
}
