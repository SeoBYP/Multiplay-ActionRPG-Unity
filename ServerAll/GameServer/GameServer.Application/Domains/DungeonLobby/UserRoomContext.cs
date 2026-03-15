using System.Threading.Channels;

namespace GameServer.Application.Domains.DungeonLobby;

public class UserRoomContext(long userId, long roomId, int capacity = 256)
{
    public long UserId { get; } = userId;

    /// <summary>방 고유 식별자 </summary>
    public long RoomId { get; } = roomId;

    public Channel<long> Outbound { get; } = Channel.CreateBounded<long>(new BoundedChannelOptions(capacity)
    {
        SingleReader = true,
        SingleWriter = false,
        FullMode = BoundedChannelFullMode.DropOldest
    });
    
    public CancellationTokenSource Cts { get; set;} = new();

    public void Stop()
    {
        if (!Cts.IsCancellationRequested) Cts.Cancel();
        Outbound.Writer.TryComplete();
    }
}