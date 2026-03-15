using GameServer.Domain.Entities.Chat;

namespace GameServer.Application.Domains.Chat.Interfaces;

public interface IChatEventStream
{
    public Task PublishAsync(string channel, ChatMessage message, CancellationToken ct);
    
    IAsyncEnumerable<ChatMessage> ReadAsync(
        IReadOnlyList<string> channels,  // 여러 채널 동시에
        string lastMessageId,
        CancellationToken ct = default);
}