using System.Text.Json;
using GameServer.Application.Domains.Chat;
using GameServer.Application.Domains.Chat.Interfaces;
using GameServer.Domain.Entities.Chat;
using GameServer.Infrastructure.MessageQueue;
using StackExchange.Redis;

namespace GameServer.Infrastructure.Domains.Chat;

public class ChatPublisher(IBroadcastChannel<ChatMessage> broadcastChannel) : IChatPublisher
{
    public async Task PublishAsync(string channel, ChatMessage message, CancellationToken ct)
    {
        await broadcastChannel.PublishAsync(channel, message, ct);
    }
}