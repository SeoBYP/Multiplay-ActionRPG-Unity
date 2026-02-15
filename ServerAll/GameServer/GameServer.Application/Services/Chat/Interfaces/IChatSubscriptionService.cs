using GameServer.Domain.Entities.Chat;

namespace GameServer.Application.Services.Chat.Interfaces;

public interface IChatSubscriptionService
{
    IAsyncEnumerable<ChatMessage> SubscribeRoomAsync(long actorUserId, long roomId, CancellationToken ct = default);
    IAsyncEnumerable<ChatMessage> SubscribeGlobalAsync(long actorUserId, CancellationToken ct = default);
}