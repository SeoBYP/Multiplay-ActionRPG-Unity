using GameServer.Application.Common;
using GameServer.Domain.Entities.Chat;

namespace GameServer.Application.Services.Chat.Interfaces;

public interface IChatService
{
    // chatType, roomId 제거 → 서버가 세션 보고 자동 결정
    Task<Result<ChatMessage>> SendMessageAsync(
        string sessionId,
        string message,
        string? targetUserNickName,
        CancellationToken ct = default);

    // 재접속용 - lastMessageId 이후 메시지 조회
    Task<IEnumerable<ChatMessage>> GetMessagesAfterAsync(
        string sessionId,
        long afterMessageId,
        CancellationToken ct = default);
}