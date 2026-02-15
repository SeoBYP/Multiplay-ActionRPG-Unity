using GameServer.Domain.Entities.Chat;

namespace GameServer.Application.Services.Chat.Interfaces;

public interface IChatService
{
    Task<ChatMessage> SendMessageAsync(string sessionId, ChatType chatType, string message, long? roomId, long? targetUserId);
    
    Task BroadcastMessageAsync(ChatMessage message);
    
    Task<ChatMessage?> GetMessageByIdAsync(long messageId);
    
    Task<IEnumerable<ChatMessage>> GetAllMessagesAsync(string sessionId);
    
    Task<IEnumerable<ChatMessage>> GetMessagesByUserIdAsync(string sessionId, long userId);
    
    Task<IEnumerable<ChatMessage>> GetMessagesByRoomIdAsync(string sessionId, long roomId);
}