using GameServer.Domain.Entities.Chat;

namespace GameServer.Infrastructure.Interfaces.Chat;

public interface IChatMessageRepository
{
    // IChatMessageRepository에 추가
    Task<IEnumerable<ChatMessage>> GetMessagesAfterAsync(long afterMessageId, CancellationToken ct = default);
    
    /// <summary>
    /// 새로운 채팅 메시지를 비동기적으로 생성합니다.
    /// </summary>
    Task<ChatMessage> CreateAsync(string senderName, ChatType chatType, string message, long? roomId, string? targetUserNickName);

    /// <summary>
    /// 식별자를 통해 채팅 메시지를 비동기적으로 조회합니다.
    /// </summary>
    /// <param name="messageId">조회할 채팅 메시지의 고유 식별자입니다.</param>
    Task<ChatMessage?> GetMessageByIdAsync(long messageId);
    
    /// <summary>
    /// 모든 채팅 메시지를 비동기적으로 조회합니다.
    /// </summary>
    Task<IEnumerable<ChatMessage>> GetAllMessagesAsync();
    
    /// <summary>
    /// 특정 사용자가 보낸 채팅 메시지 컬렉션을 비동기적으로 조회합니다.
    /// </summary>
    Task<IEnumerable<ChatMessage>> GetMessagesByUserNameAsync(string userName, int limit, long? beforeMessageId);

    /// <summary>
    /// 지정된 방 식별자의 모든 채팅 메시지를 비동기적으로 조회합니다.
    /// </summary>
    Task<IEnumerable<ChatMessage>> GetMessagesByRoomIdAsync(long roomId, int limit, long? beforeMessageId);
    
    Task<bool> DeleteAsync(long messageId);
    
    Task<bool> DeleteAllAsync();
    
    Task<bool> DeleteByUserNameAsync(string userName);
    
    Task<bool> DeleteByRoomIdAsync(long roomId);
}