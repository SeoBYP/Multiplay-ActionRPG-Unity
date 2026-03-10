using GameServer.Domain.Entities.Chat;

namespace GameServer.Application.Domains.Chat.Interfaces;

public interface IChatMessageRepository
{
    // IChatMessageRepository에 추가
    Task<IEnumerable<ChatMessage>> GetMessagesAfterAsync(long afterMessageId, string userNickname, long? currentRoomId, CancellationToken ct = default);
    
    /// <summary>
    /// 새로운 채팅 메시지를 비동기적으로 생성합니다.
    /// </summary>
    Task<ChatMessage> CreateAsync(string senderName, ChatType chatType, string message, long? roomId, string? targetUserNickName, CancellationToken ct = default);

    /// <summary>
    /// 식별자를 통해 채팅 메시지를 비동기적으로 조회합니다.
    /// </summary>
    /// <param name="messageId">조회할 채팅 메시지의 고유 식별자입니다.</param>
    Task<ChatMessage?> GetMessageByIdAsync(long messageId, CancellationToken ct = default);
    
    /// <summary>
    /// 모든 채팅 메시지를 비동기적으로 조회합니다.
    /// </summary>
    Task<IEnumerable<ChatMessage>> GetAllMessagesAsync(CancellationToken ct = default);
    
    /// <summary>
    /// 특정 사용자가 보낸 채팅 메시지 컬렉션을 비동기적으로 조회합니다.
    /// </summary>
    Task<IEnumerable<ChatMessage>> GetMessagesByUserNameAsync(string userName, int limit, long? beforeMessageId, CancellationToken ct = default);

    /// <summary>
    /// 지정된 방 식별자의 모든 채팅 메시지를 비동기적으로 조회합니다.
    /// </summary>
    Task<IEnumerable<ChatMessage>> GetMessagesByRoomIdAsync(long roomId, int limit, long? beforeMessageId, CancellationToken ct = default);
    
    Task<bool> DeleteAsync(long messageId, CancellationToken ct = default);
    
    Task<bool> DeleteAllAsync(CancellationToken ct = default);
    
    Task<bool> DeleteByUserNameAsync(string userName, CancellationToken ct = default);
    
    Task<bool> DeleteByRoomIdAsync(long roomId, CancellationToken ct = default);
}