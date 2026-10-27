using System.Text.Json;
using System.Threading.Channels;
using GameServer.Domain.Entities.Chat;
using StackExchange.Redis;

namespace GameServer.Application.Domains.Chat;

public class UserChatContext(long userId, string nickname, long roomId, int capacity = 256)
{
    public long UserId { get; } = userId;
    public string Nickname { get; } = nickname;
    public long CurrentRoomId { get; set; } = roomId; // 0 = 없음

    public Channel<ChatMessage> Outbound { get; } = Channel.CreateBounded<ChatMessage>(new BoundedChannelOptions(capacity)
    {
        SingleReader = true,
        SingleWriter = false,
        FullMode = BoundedChannelFullMode.DropOldest
    });

    public CancellationTokenSource Cts { get; } = new();
    
    public CancellationTokenSource ReadLoopCts { get; set; } = new();
    
    public void Stop()
    {
        if (!Cts.IsCancellationRequested) Cts.Cancel();
        if (!ReadLoopCts.IsCancellationRequested) ReadLoopCts.Cancel();
        Outbound.Writer.TryComplete();
    }
}