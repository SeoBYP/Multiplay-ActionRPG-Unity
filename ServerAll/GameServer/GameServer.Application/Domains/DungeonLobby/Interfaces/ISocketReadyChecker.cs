namespace GameServer.Application.Domains.DungeonLobby.Interfaces;

public interface ISocketReadyChecker
{
    Task<SocketEndpoint?> WaitForReadyAsync(long roomId, CancellationToken ct = default);
}
