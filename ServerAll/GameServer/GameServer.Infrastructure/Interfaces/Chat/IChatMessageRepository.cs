using GameServer.Domain.Entities.Chat;

namespace GameServer.Infrastructure.Interfaces.Chat;

public interface IChatMessageRepository
{
    /// <summary>
    /// 새로운 채팅 메시지를 비동기적으로 생성합니다.
    /// </summary>
    /// <param name="senderId">메시지를 생성하는 사용자의 식별자입니다.</param>
    /// <param name="chatType">채팅 유형입니다 (예: 전체, 방, 귓속말).</param>
    /// <param name="message">채팅 메시지의 내용입니다.</param>
    /// <param name="roomId">메시지가 전송되는 방의 식별자입니다 (해당하는 경우).</param>
    /// <param name="targetUserId">귓속말 메시지의 대상 사용자 식별자입니다.</param>
    /// <returns>새로 생성된 채팅 메시지를 나타내는 <see cref="ChatMessage"/> 인스턴스입니다.</returns>
    Task<ChatMessage> CreateAsync(ulong senderId, ChatType chatType, string message, long roomId, long targetUserId);

    /// <summary>
    /// 식별자를 통해 채팅 메시지를 비동기적으로 조회합니다.
    /// </summary>
    /// <param name="messageId">조회할 채팅 메시지의 고유 식별자입니다.</param>
    /// <returns>메시지를 찾은 경우 <see cref="ChatMessage"/> 인스턴스를 반환하며, 그렇지 않으면 null을 반환합니다.</returns>
    Task<ChatMessage?> GetMessageByIdAsync(long messageId);
    
    /// <summary>
    /// 모든 채팅 메시지를 비동기적으로 조회합니다.
    /// </summary>
    /// <returns>시스템의 모든 채팅 메시지를 나타내는 <see cref="ChatMessage"/> 인스턴스의 열거 가능한 컬렉션입니다.</returns>
    Task<IEnumerable<ChatMessage>> GetAllMessagesAsync();
    
    /// <summary>
    /// 특정 사용자가 보낸 채팅 메시지 컬렉션을 비동기적으로 조회합니다.
    /// </summary>
    /// <param name="userId">메시지를 조회할 사용자의 식별자입니다.</param>
    /// <returns>지정된 사용자가 보낸 <see cref="ChatMessage"/> 인스턴스의 컬렉션입니다.</returns>
    Task<IEnumerable<ChatMessage>> GetMessagesByUserIdAsync(long userId);

    /// <summary>
    /// 지정된 방 식별자의 모든 채팅 메시지를 비동기적으로 조회합니다.
    /// </summary>
    /// <param name="roomId">메시지를 조회할 방의 식별자입니다.</param>
    /// <returns>지정된 방의 메시지를 나타내는 <see cref="ChatMessage"/> 인스턴스의 컬렉션입니다.</returns>
    Task<IEnumerable<ChatMessage>> GetMessagesByRoomIdAsync(long roomId);
    
    Task<bool> DeleteAsync(long messageId);
    
    Task<bool> DeleteAllAsync();
    
    Task<bool> DeleteByUserIdAsync(long userId);
    
    Task<bool> DeleteByRoomIdAsync(long roomId);
}