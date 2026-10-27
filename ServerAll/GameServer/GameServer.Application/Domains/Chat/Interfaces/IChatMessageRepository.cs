using GameServer.Domain.Entities.Chat;

namespace GameServer.Application.Domains.Chat.Interfaces;

public interface IChatMessageRepository
{
    Task<IEnumerable<ChatMessage>> GetMessagesAfterAsync(long afterMessageId, CancellationToken ct = default);
    
    Task<ChatMessage> CreateAsync(string senderName, ChatType chatType, string message, long? roomId, string? targetUserNickName, CancellationToken ct = default);

    Task<ChatMessage?> GetMessageByIdAsync(long messageId, CancellationToken ct = default);
    
    Task<IEnumerable<ChatMessage>> GetAllMessagesAsync(CancellationToken ct = default);
    
    Task<IEnumerable<ChatMessage>> GetMessagesByUserNameAsync(string userName, int limit, long? beforeMessageId, CancellationToken ct = default);

    Task<IEnumerable<ChatMessage>> GetMessagesByRoomIdAsync(long roomId, int limit, long? beforeMessageId, CancellationToken ct = default);
    
    Task<bool> DeleteAsync(long messageId, CancellationToken ct = default);
    
    Task<bool> DeleteAllAsync(CancellationToken ct = default);
    
    Task<bool> DeleteByUserNameAsync(string userName, CancellationToken ct = default);
    
    Task<bool> DeleteByRoomIdAsync(long roomId, CancellationToken ct = default);
}