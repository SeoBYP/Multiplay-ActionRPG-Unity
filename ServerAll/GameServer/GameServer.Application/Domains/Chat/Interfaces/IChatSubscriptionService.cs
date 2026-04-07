namespace GameServer.Application.Domains.Chat.Interfaces;

public interface IChatSubscriptionService
{
    Task<UserChatContext?> ConnectAsync(string sessionId, CancellationToken ct);
    
    Task UpdateRoomSubscriptionAsync(string sessionId, long roomId, CancellationToken ct);

    Task DisconnectAsync(UserChatContext ctx, CancellationToken ct = default);
}