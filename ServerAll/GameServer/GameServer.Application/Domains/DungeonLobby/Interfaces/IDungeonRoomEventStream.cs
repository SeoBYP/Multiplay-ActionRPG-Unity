namespace GameServer.Application.Domains.DungeonLobby.Interfaces;

public interface IDungeonRoomEventStream
{
    Task PublishAsync(long roomId, CancellationToken ct = default);
    IAsyncEnumerable<long> ReadAsync(long roomId, string lastEventId, CancellationToken ct = default);
}