using System.Threading.Channels;
using GameServer.Application.Domains.Chat.Interfaces;
using GameServer.Domain.Entities.Chat;
using Shared.Infrastructure.MessageQueue;

namespace GameServer.Infrastructure.Domains.Chat;

public class ChatEventStream(IBroadcastChannel<ChatMessage> broadcastChannel) : IChatEventStream
{
    public async Task PublishAsync(string channel, ChatMessage message, CancellationToken ct)
    {
        await broadcastChannel.PublishAsync(channel, message, ct);
    }
    
    
    public async IAsyncEnumerable<ChatMessage> ReadAsync(IReadOnlyList<string> channels, string lastMessageId, CancellationToken ct = default)
    {
        var merged = Channel.CreateUnbounded<ChatMessage>();
        
        var tasks = channels.Select(ch => ReadChannelAsync(ch, lastMessageId, merged.Writer, ct)).ToList();

        _ = Task.WhenAll(tasks).ContinueWith(_ => merged.Writer.TryComplete(), ct);

        await foreach (var msg in merged.Reader.ReadAllAsync(ct))
            yield return msg;
    }
    
    private async Task ReadChannelAsync(string channel, string lastMessageId,
        ChannelWriter<ChatMessage> writer, CancellationToken ct)
    {
        await foreach (var (_, msg) in broadcastChannel.ReadAsync(channel, lastMessageId, ct))
            writer.TryWrite(msg);
    }
}