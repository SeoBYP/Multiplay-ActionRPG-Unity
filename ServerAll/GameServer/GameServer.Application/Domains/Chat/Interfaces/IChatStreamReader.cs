using GameServer.Domain.Entities.Chat;

namespace GameServer.Application.Domains.Chat.Interfaces;

public interface IChatStreamReader
{
    IAsyncEnumerable<ChatMessage> ReadAsync(
        IReadOnlyList<string> channels,  // 여러 채널 동시에
        string lastMessageId,
        CancellationToken ct = default);
}