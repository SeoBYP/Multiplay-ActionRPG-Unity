using System.Runtime.CompilerServices;
using GameServer.Application.Domains.DungeonLobby;
using GameServer.Application.Domains.DungeonLobby.Interfaces;

namespace GameServer.Infrastructure.Domains.DungeonRoom;

public class DungeonRoomEventStream(DungeonRoomBroadcastChannel broadcastChannel) : IDungeonRoomEventStream
{
    public async Task PublishAsync(long roomId, CancellationToken ct = default)
    {
        var channel = RoomChannels.RoomChannel(roomId);
        await broadcastChannel.PublishAsync(channel, roomId, ct);
    }

    public async IAsyncEnumerable<long> ReadAsync(long roomId, string lastEventId, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var channel = RoomChannels.RoomChannel(roomId);
        await foreach (var (_, msg) in broadcastChannel.ReadAsync(channel, lastEventId, ct))
        {
            yield return msg;
        }
    }
}