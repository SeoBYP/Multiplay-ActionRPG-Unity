using GameServer.Application.Common;
using GameServer.Domain.Entities.Chat;

namespace GameServer.Application.Services.Chat.Interfaces;

public interface IChatService
{
    Task<Result<ChatMessage>> SendMessageAsync(
        string sessionId, ChatType chatType, string message, long? roomId, long? targetUserId, CancellationToken ct = default);

    Task<Result<ChatMessage>> GetMessageByIdAsync(
        string sessionId, long messageId, CancellationToken ct = default);

    Task<Result<IReadOnlyList<ChatMessage>>> GetMessagesByRoomAsync(
        string sessionId, long roomId, int limit = 50, long? beforeMessageId = null, CancellationToken ct = default);
    
    Task<Result<IReadOnlyList<ChatMessage>>> GetMessagesByUserAsync(
        string sessionId, long userId, int limit = 50, long? beforeMessageId = null, CancellationToken ct = default);
}