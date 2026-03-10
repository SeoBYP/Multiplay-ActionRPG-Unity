namespace GameServer.Application.Domains.DungeonLobby.Interfaces;

public interface IDungeonLobbySubscriptionService
{
    Task<UserRoomContext?> SubscribeAsync(string sessionId, long roomId, CancellationToken ct);
    
    Task PublishAsync(long roomId, CancellationToken ct);
    
    Task UnsubscribeAsync(UserRoomContext ctx, CancellationToken ct = default);
}