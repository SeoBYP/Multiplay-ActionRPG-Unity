using System.Text.Json;
using System.Threading.Channels;
using GameServer.Domain.Entities.Chat;
using StackExchange.Redis;

namespace GameServer.Application.Domains.Chat;

public class UserChatContext(long userId, string nickname, long roomId, int capacity = 256)
{
    public long UserId { get; } = userId;
    public string Nickname { get; } = nickname;
    public long CurrentRoomId { get; set; } = roomId; // 0 = none

    public Channel<ChatMessage> Outbound { get; } = Channel.CreateBounded<ChatMessage>(new BoundedChannelOptions(capacity)
    {
        SingleReader = true,
        SingleWriter = false,
        FullMode = BoundedChannelFullMode.DropOldest
    }); // gRPC 스트림 버퍼 → 여전히 필요

    public CancellationTokenSource Cts { get; } = new(); // 연결 종료 제어 → 여전히 필요
    
    public CancellationTokenSource ReadLoopCts { get; set; } = new();
    
    public void Stop()
    {
        if (!Cts.IsCancellationRequested) Cts.Cancel();
        if (!ReadLoopCts.IsCancellationRequested) ReadLoopCts.Cancel();
        Outbound.Writer.TryComplete();
    }
}