using System.Threading.Channels;
using StackExchange.Redis;

namespace GameServer.Application.Domains.DungeonLobby;

public class UserRoomContext
{
    public long UserId { get; }
    /// <summary>방 고유 식별자 </summary>
    public long RoomId { get; }

    public Channel<long> Outbound { get; }
    public ISubscriber Subscriber { get; }
    public Action<RedisChannel, RedisValue> OnRedisMessage { get; }
    
    public CancellationTokenSource Cts { get; } = new();
    
    public UserRoomContext(long userId, long roomId, 
        ISubscriber subscriber, int capacity = 256)
    {
        RoomId = roomId;
        UserId = userId;
        Subscriber = subscriber;

        Outbound = Channel.CreateBounded<long>(new BoundedChannelOptions(capacity)
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
                if (long.TryParse(value.ToString(), out var parsedRoomId))
                {
                    Console.WriteLine($"[역직렬화] RoomId={parsedRoomId}"); // ← 추가
                    Outbound.Writer.TryWrite(parsedRoomId);
                    return;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"[역직렬화 실패] {e.Message}"); // ← 추가
            }
        };
    }

    public void Stop()
    {
        if (!Cts.IsCancellationRequested) Cts.Cancel();
        Outbound.Writer.TryComplete();
    }
}