using GameServer.Domain.Entities;

namespace GameServer.Application.Services.DungeonLobby.Interfaces;

public interface IDungeonLobbySubscriptionService
{
    Task<UserRoomContext?> SubscribeAsync(string sessionId, long roomId, CancellationToken ct);
    
    Task PublishAsync(long roomId, CancellationToken ct);
    
    Task UnsubscribeAsync(UserRoomContext ctx, CancellationToken ct = default);
}