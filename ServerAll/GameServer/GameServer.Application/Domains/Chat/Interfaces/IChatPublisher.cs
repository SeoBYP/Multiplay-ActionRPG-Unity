using GameServer.Domain.Entities.Chat;

namespace GameServer.Application.Domains.Chat.Interfaces;

public interface IChatPublisher
{
    Task PublishAsync(string channel, ChatMessage message, CancellationToken ct);
}