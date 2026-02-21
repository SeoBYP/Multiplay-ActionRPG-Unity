using GameServer.Domain.Entities.Chat;

namespace GameServer.Application.Services.Chat.Interfaces;

public interface IChatSubscriptionService
{
    IAsyncEnumerable<ChatMessage> SubscribeGlobalAsync(string sessionId, CancellationToken ct = default);
    IAsyncEnumerable<ChatMessage> SubscribeRoomAsync(string sessionId, long roomId, CancellationToken ct = default);
    IAsyncEnumerable<ChatMessage> SubscribeWhisperAsync(string sessionId, CancellationToken ct = default);
}