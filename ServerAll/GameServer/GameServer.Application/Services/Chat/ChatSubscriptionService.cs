using GameServer.Application.Services.Chat.Interfaces;
using GameServer.Domain.Entities.Chat;

namespace GameServer.Application.Services.Chat;

public class ChatSubscriptionService : IChatSubscriptionService
{
    public IAsyncEnumerable<ChatMessage> SubscribeRoomAsync(long actorUserId, long roomId, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public IAsyncEnumerable<ChatMessage> SubscribeGlobalAsync(long actorUserId, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }
}