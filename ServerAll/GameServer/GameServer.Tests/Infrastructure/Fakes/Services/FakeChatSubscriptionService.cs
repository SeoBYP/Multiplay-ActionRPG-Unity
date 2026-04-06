using GameServer.Application.Domains.Chat;
using GameServer.Application.Domains.Chat.Interfaces;

namespace GameServer.Tests.Infrastructure.Fakes.Services;

public class FakeChatSubscriptionService : IChatSubscriptionService
{
    public Task<UserChatContext?> ConnectAsync(string sessionId, CancellationToken ct)
    {
        return Task.FromResult<UserChatContext?>(null);
    }

    public Task SwitchRoomAsync(string sessionId, long roomId, CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    public Task DisconnectAsync(UserChatContext ctx, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }
}
