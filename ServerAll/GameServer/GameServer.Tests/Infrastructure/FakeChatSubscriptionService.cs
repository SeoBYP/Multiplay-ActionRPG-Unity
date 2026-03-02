using GameServer.Application.Services.Chat;
using GameServer.Application.Services.Chat.Interfaces;
using GameServer.Domain.Entities.Chat;

namespace GameServer.Tests.Infrastructure;

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
