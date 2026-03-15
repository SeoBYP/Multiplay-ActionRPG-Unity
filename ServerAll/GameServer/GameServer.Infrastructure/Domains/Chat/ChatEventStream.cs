using System.Threading.Channels;
using GameServer.Application.Domains.Chat.Interfaces;
using GameServer.Domain.Entities.Chat;
using GameServer.Infrastructure.MessageQueue;

namespace GameServer.Infrastructure.Domains.Chat;

public class ChatEventStream(IBroadcastChannel<ChatMessage> broadcastChannel) : IChatEventStream
{
    public async Task PublishAsync(string channel, ChatMessage message, CancellationToken ct)
    {
        await broadcastChannel.PublishAsync(channel, message, ct);
    }
    
    
    public async IAsyncEnumerable<ChatMessage> ReadAsync(IReadOnlyList<string> channels, string lastMessageId, CancellationToken ct = default)
    {
        // 결과를 합쳐서 반환할 내부 채널
        var merged = Channel.CreateUnbounded<ChatMessage>();
        
        // 각 채널마다 병렬 읽기 Task 시작
        var tasks = channels.Select(ch => ReadChannelAsync(ch, lastMessageId, merged.Writer, ct)).ToList();

        // 모든 Task 끝나면 merged 닫기
        _ = Task.WhenAll(tasks).ContinueWith(_ => merged.Writer.TryComplete(), ct);

        // merged에서 꺼내서 반환
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