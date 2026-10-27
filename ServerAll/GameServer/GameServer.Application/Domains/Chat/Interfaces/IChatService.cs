using GameServer.Application.Common;
using GameServer.Domain.Entities.Chat;

namespace GameServer.Application.Domains.Chat.Interfaces;

public interface IChatService
{
    // chatType·roomId 는 서버가 세션을 보고 결정한다
    Task<Result<ChatMessage>> SendMessageAsync(
        string sessionId,
        string message,
        string? targetUserNickName,
        CancellationToken ct = default);

    // 재접속용 — afterMessageId 이후 메시지
    Task<IEnumerable<ChatMessage>> GetMessagesAfterAsync(
        string sessionId,
        long afterMessageId,
        CancellationToken ct = default);

    Task<Result<ChatMessage>> GetMessageByIdAsync(
        string sessionId,
        long messageId,
        CancellationToken ct = default);

    Task<Result<List<ChatMessage>>> GetMessagesByRoomAsync(
        string sessionId,
        long roomId,
        int limit = 50,
        long? beforeMessageId = null,
        CancellationToken ct = default);

    Task<Result<List<ChatMessage>>> GetMessagesByUserAsync(
        string sessionId,
        string userName,
        int limit = 50,
        long? beforeMessageId = null,
        CancellationToken ct = default);
}