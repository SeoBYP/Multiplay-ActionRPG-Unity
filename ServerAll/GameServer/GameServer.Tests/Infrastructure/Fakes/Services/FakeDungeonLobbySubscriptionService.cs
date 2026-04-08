using GameServer.Application.Domains.DungeonLobby;
using GameServer.Application.Domains.DungeonLobby.Interfaces;

namespace GameServer.Tests.Infrastructure.Fakes.Services;

public class FakeDungeonLobbySubscriptionService : IDungeonLobbySubscriptionService
{
    public Task<UserRoomContext> SubscribeAsync(long userId, long roomId, CancellationToken ct)
    {
        return Task.FromResult(new UserRoomContext(userId, roomId));
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
