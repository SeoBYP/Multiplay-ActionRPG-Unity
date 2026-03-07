using GameServer.Application.Services.DungeonLobby;
using GameServer.Application.Services.DungeonLobby.Interfaces;

namespace GameServer.Tests.Infrastructure;

public class FakeDungeonLobbySubscriptionService : IDungeonLobbySubscriptionService
{
    public Task<UserRoomContext?> SubscribeAsync(string sessionId, long roomId, CancellationToken ct)
    {
        return Task.FromResult<UserRoomContext?>(null);
    }

    public Task PublishAsync(long roomId, CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    public Task UnsubscribeAsync(UserRoomContext ctx, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }
}
