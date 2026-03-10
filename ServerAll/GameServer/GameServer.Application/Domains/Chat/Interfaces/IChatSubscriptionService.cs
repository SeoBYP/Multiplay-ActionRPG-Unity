namespace GameServer.Application.Domains.Chat.Interfaces;

public interface IChatSubscriptionService
{
    Task<UserChatContext?> ConnectAsync(string sessionId, CancellationToken ct);

    // ctx 인자로 받지 않고 sessionId 기반으로 변경
    Task SwitchRoomAsync(string sessionId, long roomId, CancellationToken ct);

    Task DisconnectAsync(UserChatContext ctx, CancellationToken ct = default);
}