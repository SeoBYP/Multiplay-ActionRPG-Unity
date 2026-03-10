using System.Text.Json;
using System.Threading.Channels;
using GameServer.Domain.Entities.Chat;
using StackExchange.Redis;

namespace GameServer.Application.Domains.Chat;

public class UserChatContext
{
    public long UserId { get; }
    public string Nickname { get; }
    public long CurrentRoomId { get; set; } // 0 = none

    public Channel<ChatMessage> Outbound { get; }
    public ISubscriber Subscriber { get; }
    public Action<RedisChannel, RedisValue> OnRedisMessage { get; }
    public HashSet<RedisChannel> SubscribedChannels { get; } = new();
    public CancellationTokenSource Cts { get; } = new();


    public UserChatContext(long userId, string nickname, long roomId, ISubscriber subscriber, int capacity = 256)
    {
        UserId = userId;
        Nickname = nickname;
        CurrentRoomId = roomId;

        Subscriber = subscriber;

        Outbound = Channel.CreateBounded<ChatMessage>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });

        OnRedisMessage = (_, value) =>
        {
            if (value.IsNullOrEmpty) return;
            try
            {
                Console.WriteLine($"[Redis 수신] {value}"); // ← 추가
                var msg = JsonSerializer.Deserialize<ChatMessage>(value.ToString());
                Console.WriteLine($"[역직렬화] MessageId={msg?.MessageId}, Message={msg?.Message}"); // ← 추가
                if (msg != null) Outbound.Writer.TryWrite(msg);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[역직렬화 실패] {ex.Message}"); // ← 추가
            }
        };
    }
    
    public void Stop()
    {
        if (!Cts.IsCancellationRequested) Cts.Cancel();
        Outbound.Writer.TryComplete();
    }
}